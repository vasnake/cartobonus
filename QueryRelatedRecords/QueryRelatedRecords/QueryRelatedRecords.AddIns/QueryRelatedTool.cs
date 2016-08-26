using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit;
using ESRI.ArcGIS.Client.FeatureService;
using System.Text.RegularExpressions;

using System.Windows.Browser;
//using System.Reflection;

namespace QueryRelatedRecords.AddIns {
	[Export(typeof(ICommand))]
	[DisplayName("Query Related Records")]
	[Description("Query related records for a feature")]
	[Category("Query")]
	[DefaultIcon("/ESRI.ArcGIS.Mapping.Controls;component/images/icon_tools16.png")]
	public class QueryRelatedTool: ICommand {

		/// <summary>
		/// I want FSM (http://stackoverflow.com/questions/5923767/simple-state-machine-example-in-c)
		/// http://stackoverflow.com/questions/5923767/simple-state-machine-example-in-c/5924286#5924286
		/// </summary>
		public QueryRelatedTool() {
			// Initialize the QueryTask
			queryTask = new QueryTask();
			queryTask.ExecuteRelationshipQueryCompleted += QueryTask_ExecuteRelationshipQueryCompleted;
			queryTask.Failed += QueryTask_Failed;
			relationsListForm = new VShortList(this);
			flList = new Dictionary<string, mwb02.AddIns.VLayer>();
			try {
				MapApplication.Current.Map.Layers.CollectionChanged -= Layers_CollectionChanged;
				MapApplication.Current.Map.Layers.CollectionChanged += Layers_CollectionChanged;
				log(string.Format("Constructor OK"));
			}
			catch(Exception ex) {
				log(string.Format("Constructor, error {0}", ex.Message));
			}
		} // public QueryRelatedTool()


		#region layers collection

		void Layers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
			try {
				if(e.NewItems == null) { return; }

				log(string.Format("Layers_CollectionChanged, new items {0}", e.NewItems.Count));
				foreach(var i in e.NewItems) {
					log(string.Format("Layers_CollectionChanged, new layer type '{0}'", i.GetType())); // 'ESRI.ArcGIS.Client.GraphicsLayer'
					// extract featurelayers, init them and add to layerslist for further needs
					var vl = new mwb02.AddIns.VLayer(i as Layer);
					//log(string.Format("Layers_CollectionChanged, new layer JSON '{0}'", vl.toJson()));
					log(string.Format("Layers_CollectionChanged, new layer details '{0}' '{1}'", vl.ID, vl.lyrName));

					if(vl.lyrType == "FeatureLayer") {
						log(string.Format("Layers_CollectionChanged, featurelayer added"));
						addFL2Collection(vl);
					}
					else if(vl.lyrType == "ArcGISDynamicMapServiceLayer") {
						log(string.Format("Layers_CollectionChanged, dynamapservicelayer added, need to extract FLs"));
						getFLsFromDynamapservicelyr(vl);
					}
					// todo: else if(vl.lyrType == "ArcGISTiledMapServiceLayer") {}
					else {
						log(string.Format("Layers_CollectionChanged, unsupported layer"));
					}
				} // foreach added layer
			}
			catch(Exception ex) {
				log(string.Format("Layers_CollectionChanged, error {0}", ex.Message));
			}
		} // void Layers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)


		/// <summary>
		/// get list of featurelayers from ArcGISDynamicMapServiceLayer
		/// </summary>
		/// <param name="lyr"></param>
		/// <returns></returns>
		private void getFLsFromDynamapservicelyr(mwb02.AddIns.VLayer lyr) {
			if(lyr.lyrType == "ArcGISDynamicMapServiceLayer") {
				var dl = lyr.lyr as ArcGISDynamicMapServiceLayer;

				dl.GetAllDetails(
					(layeritems, ex) => {
						if(ex == null) {
							log(string.Format("getFLsFromDynamapservicelyr, GetAllDetails, lyr {0}, items {1}", lyr.lyrUrl, layeritems.Count));
							foreach(var kvp in layeritems) {
								var fli = kvp.Value; //FeatureLayerInfo
								log(string.Format("getFLsFromDynamapservicelyr, GetAllDetails, lyrid {0}, lyrname {1}, lyrtype {2}",
									fli.Id, fli.Name, fli.Type));

								if(fli.Type.Contains("Feature")) {
									var vfl = lyr.getSubLayer(fli.Id);
									if(vfl == null) { log(string.Format("getFLsFromDynamapservicelyr, GetAllDetails, vfl=null")); }
									else addFL2Collection(vfl);
								} // if sublayer is FeatureLayer
							} // foreach layerinfo
						} // no errors on init
						else {
							log(string.Format("getFLsFromDynamapservicelyr, GetAllDetails fail, lyr {0}, msg '{1}'",
								lyr.lyrUrl, ex.Message));
						}
					}); // getalldetails callback

			} // if ArcGISDynamicMapServiceLayer
			else {
				log(string.Format("getFLsFromDynamapservicelyr, lyrtype != ArcGISDynamicMapServiceLayer, {0}", lyr.lyrType));
			}
			return;
		} // private void getFLsFromDynamapservicelyr(mwb02.AddIns.VLayer lyr)


		/// <summary>
		/// initialize featurelayer if it's not, add FL to list
		/// </summary>
		/// <param name="lyr"></param>
		private void addFL2Collection(mwb02.AddIns.VLayer vfl) {
			if(vfl.lyrType == "FeatureLayer") {
				var featurelayer = vfl.lyr as FeatureLayer;
				if(featurelayer.LayerInfo == null) {
					log(string.Format("addFL2Collection, FL not inited {0}", vfl.lyrUrl));
					//featurelayer.Initialize();

					// call init sequence
					featurelayer.Initialized += (sender, eventargs) => {
						if(featurelayer.InitializationFailure == null) {
							var lyrinfo = featurelayer.LayerInfo;
							if(lyrinfo == null) {
								log(string.Format("addFL2Collection, Initialize did't work, FL info is null, {0}", vfl.lyrUrl));
							}
							else { // do the job
								log(string.Format("addFL2Collection, Initialize, FL inited, {0}", vfl.lyrUrl));
								addFL2Collection(vfl);
							}
						} // init ok
						else { // init error
							log(string.Format("addFL2Collection, Initialize, error, lyr (0), msg {3}",
								vfl.lyrUrl, featurelayer.InitializationFailure.Message));
						}
					}; // lyr initialized callback
					featurelayer.Initialize();

				} // if(featurelayer.LayerInfo == null)
				else { // FL inited
					if(!flList.ContainsKey(vfl.lyrUrl)) {
						vfl.initRelations();
						flList.Add(vfl.lyrUrl, vfl);
						log(string.Format("addFL2Collection, FLs count {2}, FL {0} relations {1}", vfl.lyrUrl, featurelayer.LayerInfo.Relationships.Count(), flList.Count));
						if(CanExecuteChanged != null) { CanExecuteChanged(this, EventArgs.Empty); }
					}
				}
			} // if FeatureLayer
			else {
				log(string.Format("addFL2Collection, lyrtype != FeatureLayer, {0}", vfl.lyrUrl));
			}
		} // private void addFL2Collection(mwb02.AddIns.VLayer lyr)

		#endregion layers collection


		#region Member Variables

		private Graphic inputFeature; // Feature to get related records for.
		private QueryTask queryTask; // Query task for querying related records.
		private OnClickPopupInfo popupInfo; // Information about the feature that was clicked.
		private mwb02.AddIns.VLayer relatesLayer; // The layer to query for related features.
		private mwb02.AddIns.VLayer resultsLayer; // The layer containing the related features
		private RelationshipResult queryResult; // The results from the Query task.
		private const string CONTAINER_NAME = "FeatureDataGridContainer"; // Provides access to the Attribute table in the layout.
		private BusyIndicator indicator; // The busy indicator to show while the Query task is in progress.
		private Grid attributeGrid; // Attribute grid in the pop-up. The busy indicator is added to this attribute grid.
		private string objectID; // The name of the ObjectID field in the layer.
		public VShortList relationsListForm; // form for choose desired relation
		private mwb02.AddIns.VRelationInfo relationInfo; // relation id, name, table id
		private Dictionary<string, mwb02.AddIns.VLayer> flList; // list of all FeatureLayers
		private bool isBusy = false; // wait for async tasks

		#endregion

		#region ICommand members

		/// <summary>
		/// Executes the relationship query against the layer.
		/// </summary>
		/// <param name="parameter">The OnClickPopupInfo from the layer.</param>
		public void Execute(object parameter) {
			try {
				doExecute(parameter);
			}
			catch(Exception ex) {
				log(string.Format("Execute, catch error '{0}'", ex.Message));
				MessageBox.Show(string.Format("Показать связанные записи невозможно." +
					"\n Error: {0}", ex.Message));
			}
		} // public void Execute(object parameter)


		/// <summary>
		/// Executes the relationship query against the layer.
		/// </summary>
		/// <param name="parameter">The OnClickPopupInfo from the layer.</param>
		public void doExecute(object parameter) {
			// The plan is:
			// Get the featurelayer and clicked feature from the pop-up.
			// The PopupItem property of OnClickPopupInfo provides information about the item currently shown in the pop-up.
			// Then get feature ID value and put it into ExecuteRelationshipQueryAsync task.
			// Then get related records ID's and create FeatureLayer from related table/feature class, filtered by that ID's.
			// Then show grid for that layer.
			popupInfo = parameter as OnClickPopupInfo;
			inputFeature = popupInfo.PopupItem.Graphic;
			var lyr = new mwb02.AddIns.VLayer(popupInfo.PopupItem.Layer);

			// print layer info to console
			log(string.Format("Execute, layer type '{0}', popupInd '{1}', popupDescr '{2}', lyrID '{3}', lyrName '{4}', title '{5}'",
				popupInfo.PopupItem.Layer.GetType(), // 'ESRI.ArcGIS.Client.ArcGISDynamicMapServiceLayer'
				popupInfo.SelectedIndex, popupInfo.SelectionDescription,
				popupInfo.PopupItem.LayerId, popupInfo.PopupItem.LayerName, popupInfo.PopupItem.Title));
			// SelectedIndex - index 0-n for found features.
			// SelectionDescription - note about current record for user, '2 from 2' for example.
			// lyrID '0' - sublayer id for ArcGISDynamicMapServiceLayer
			// lyrName 'Аэродромы и вертодромы' - sublayer name for ArcGISDynamicMapServiceLayer
			log(string.Format("Execute, lyrType '{0}', lyrUrl '{1}'", lyr.lyrType, lyr.lyrUrl));
			// layer type 'ESRI.ArcGIS.Client.ArcGISDynamicMapServiceLayer', popupInd '0', popupDescr '1 из 2', lyrID '0', lyrName 'Wells', title 'UNKNOWN'
			// lyrType 'ArcGISDynamicMapServiceLayer', lyrUrl 'http://sampleserver3.arcgisonline.com/ArcGIS/rest/services/Petroleum/KSPetro/MapServer'
			log(string.Format("Execute, inputFeature.Attributes.Count '{0}'", inputFeature.Attributes.Count));

			// we need FeatureLayer
			if(lyr.lyrType == "FeatureLayer") {
				relatesLayer = lyr; // The layer to get related records for. This is used to get the RelationshipID and Query url.
			}
			else if(lyr.lyrType == "ArcGISDynamicMapServiceLayer") {
				var rLyr = getSubLayer(lyr, popupInfo.PopupItem.LayerId) as FeatureLayer;
				if(relatesLayer != null && relatesLayer.lyrUrl == rLyr.Url) {
					; // we here after relatesLayer.Initialized
				}
				else { // init new FeatureLayer
					relatesLayer = new mwb02.AddIns.VLayer(rLyr);
					relatesLayer.lyr.Initialized += (a, b) => {
						if(relatesLayer.lyr.InitializationFailure == null) {
							var info = (relatesLayer.lyr as FeatureLayer).LayerInfo;
							log(string.Format("Execute, relatesLayer.InitializationFailure == null, info '{0}'", info));
							Execute(parameter);
						}
					}; // callback
					relatesLayer.lyr.Initialize();
					log(string.Format("Execute, relatesLayer.Initialize called, wait..."));
					return;
				} // init new FeatureLayer
			} // if(lyr.lyrType == "ArcGISDynamicMapServiceLayer")
			else {
				throw new Exception("Тип слоя должен быть или FeatureLayer или ArcGISDynamicMapServiceLayer");
			}

			// we have inited FeatureLayer now
			if(relatesLayer.getFL().LayerInfo == null) {
				throw new Exception(string.Format("Execute, relatesLayer.LayerInfo == null"));
			}
			var clickedLayer = relatesLayer;
			var storedFL = this.flList[clickedLayer.lyrUrl];
			// check FeatureLayer info
			log(string.Format("Execute, relatesLayer lyrType '{0}', lyrUrl '{1}'", clickedLayer.lyrType, clickedLayer.lyrUrl));

			// get relationship id
			var rels = relatesLayer.getFL().LayerInfo.Relationships;
			log(string.Format("Execute, getrelid.1, rels.count '{0}'", rels.Count()));
			if(rels.Count() <= 0) {
				log(string.Format("Execute, relationships.count <= 0"));
				throw new Exception(string.Format("У выбранного слоя нет связей с другими таблицами"));
			}
			else if(rels.Count() > 1) {
				log(string.Format("Execute, relationships.count > 1"));
				if(relationsListForm.relationsList.Count > 0) {
					// continue after user input
					relationInfo = relationsListForm.listBox1.SelectedItem as mwb02.AddIns.VRelationInfo;
					relationsListForm.relationsList.Clear();
				} // user select relID already
				else {
					// new query
					relationInfo = null;
					foreach(var r in rels) {
						var ri = storedFL.getRelation(r);
						if(ri == null) ri = new mwb02.AddIns.VRelationInfo(r);
						ri.oid = clickedLayer.getOID(inputFeature);
						if(ri.oid == -1) {
							log(string.Format("featurelayer.getOID returns invalid OID; {0}", clickedLayer.lyrUrl));
							continue;
						}
						relationsListForm.relationsList.Add(ri);
					}
					relationsListForm.listBox1.SelectedItem = relationsListForm.relationsList.First();
					MapApplication.Current.ShowWindow("Relations",
						relationsListForm,
						false, // ismodal
						(sender, canceleventargs) => { log("relationsListForm onhidINGhandler"); }, // onhidinghandler
						(sender, eventargs) => {
							log("relationsListForm onhidEhandler");
							//if(relationsListForm.listBox1.SelectedItem != null) 
							Execute(parameter);
						}, // onhidehandler
						WindowType.Floating
					);
					return; // wait for user input
				} // new query				
			} // rels.count > 1
			else { // rels.count == 1
				log(string.Format("Execute, relationships.count = 1"));
				relationInfo = new mwb02.AddIns.VRelationInfo(rels.First());
			}

			// ok, we get relation info now
			if(relationInfo == null) throw new Exception("Не указана связанная таблица");
			log(string.Format("Execute, getrelid.2, relationshipID '{0}', rels.count '{1}'", relationInfo.id, rels.Count()));

			// Get the name of the ObjectID field.
			objectID = clickedLayer.getOIDFieldnameOrAlias();

			// get key value
			int objIdValue = clickedLayer.getOID(inputFeature);
			if(objIdValue == -1) {
				// Attributes key = 'Object ID' but ObjectIdField = 'OBJECTID'
				var ks = string.Join(", ", inputFeature.Attributes.Keys); // inputFeature.AttributesKeys='Object ID, Shape, Field KID,
				var vs = string.Join(", ", inputFeature.Attributes.Values);
				log(string.Format("Execute, inputFeature.AttributesKeys='{0}', values='{1}'", ks, vs));
				throw new Exception(string.Format("Поле OBJECTID не содержит целого числа"));
			}
			log(string.Format("Execute, objIdValue.int='{0}'", objIdValue));

			// Input parameters for QueryTask
			RelationshipParameter relationshipParameters = new RelationshipParameter() {
				ObjectIds = new int[] { objIdValue },
				OutFields = new string[] { "*" }, // Return all fields
				ReturnGeometry = true, // Return the geometry so that features can be displayed on the map if applicable
				RelationshipId = relationInfo.id, // Obtain the desired RelationshipID from the Service Details page. Here it takes the first relationship it finds if there is more than one.
				OutSpatialReference = MapApplication.Current.Map.SpatialReference
			};
			log(string.Format("Execute, relationshipParameters set"));

			// Specify the Feature Service url for the QueryTask.
			if(queryTask.IsBusy) throw new Exception("Выполняется предыдущий запрос, попробуйте позже");
			queryTask.Url = relatesLayer.lyrUrl;

			//  Execute the Query Task with specified parameters
			queryTask.ExecuteRelationshipQueryAsync(relationshipParameters);

			// Find the attribute grid in the Pop-up and insert the BusyIndicator
			attributeGrid = Utils.FindChildOfType<Grid>(popupInfo.AttributeContainer, 3);
			indicator = new BusyIndicator();
			if(attributeGrid != null) {
				// Add the Busy Indicator
				attributeGrid.Children.Add(indicator);
				indicator.IsBusy = true;
			}

			log(string.Format("Execute, completed, wait for QueryTask_ExecuteRelationshipQueryCompleted"));
		} // public void doExecute(object parameter)


		/// <summary>
		/// Checks whether the Query Related Records tool can be used. 
		/// </summary>
		/// <param name="parameter">The OnClickPopupInfo from the layer.</param>
		public bool CanExecute(object parameter) {
			try {
				popupInfo = parameter as OnClickPopupInfo;
				if(popupInfo == null || popupInfo.PopupItem == null || popupInfo.PopupItem.Graphic == null) {
					//log(string.Format("CanExecute false, popupinfo or popupitem is null"));
					return false;
				}

				if(MapApplication.Current == null || MapApplication.Current.Map == null
					|| MapApplication.Current.Map.Layers == null) {
					//log(string.Format("CanExecute false, map or layers is null"));
					return false;
				}

				var lyr = new mwb02.AddIns.VLayer(popupInfo.PopupItem.Layer);
				if(lyr.lyrType.Contains("DynamicMapServiceLayer")) {
					lyr = lyr.getSubLayer(popupInfo.PopupItem.LayerId);
				}
				if(!lyr.lyrType.Contains("Feature")) {
					log(string.Format("CanExecute false, layer is not FeatureLayer, {0}", lyr.lyrType));
					return false;
				}

				// http://www.dotnetperls.com/dictionary
				var vfl = new mwb02.AddIns.VLayer();
				if(!flList.TryGetValue(lyr.lyrUrl, out vfl)) {
					log(string.Format("CanExecute false, layer not in list {0}", lyr.lyrUrl));
					addFL2Collection(lyr);
					return false;
				}

				var fl = vfl.lyr as FeatureLayer;
				if(fl.LayerInfo != null && fl.LayerInfo.Relationships != null && fl.LayerInfo.Relationships.Count() > 0) {
					var relrecs = countRelatedRecords(vfl, popupInfo.PopupItem.Graphic);
					if(relrecs != 0) return true;
					log(string.Format("CanExecute false, relatedrecords.count = 0. {0}", lyr.lyrUrl));
					return false;
				}

				log(string.Format("CanExecute false, layerinfo or relations is null {0}", lyr.lyrUrl));
				return false;
			}
			catch(Exception ex) {
				log(string.Format("CanExecute error {0}\n{1}", ex.Message, ex.StackTrace));
				return false;
			}
		} // public bool CanExecute(object parameter)


		public event EventHandler CanExecuteChanged; // Event that can be raised when the executability of the command changes. When this event is raised, the Viewer will invoke the CanExecute method, allowing the add-in to update the state of the tool's button on the toolbar. 
		//if (CanExecuteChanged != null){CanExecuteChanged(this, EventArgs.Empty);}

		#endregion ICommand members


		#region Event Handlers
		/// <summary>
		/// Handle successful query task and create FeatureLayer from related table/FC
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void QueryTask_ExecuteRelationshipQueryCompleted(object sender, RelationshipEventArgs e) {
			// queryResult used once the results layer is initialized
			queryResult = e.Result;

			// Create a new url for the related table using the querytaskUrl and the RelatedTableId.
			string resultsUrl = relatesLayer.lyrUrl;
			var lastSlash = resultsUrl.LastIndexOf("/"); // -1?
			resultsUrl = string.Format("{0}/{1}", resultsUrl.Substring(0, lastSlash), relationInfo.tableId);
			log(string.Format("QueryTask_ExecuteRelationshipQueryCompleted, resultsUrl='{0}'", resultsUrl));

			// Create a FeatureLayer for the results based on the url of the related records. 
			var resFL = new FeatureLayer() {
				Url = resultsUrl
			};
			resFL.OutFields.Add("*");

			// Initialize the resultsLayer to populate layer metadata (LayerInfo) so the OID field can be retrieved.
			resFL.Initialized += resultsLayer_Initialized;
			resFL.Initialize();
			resultsLayer = new mwb02.AddIns.VLayer(resFL);

			log(string.Format("QueryTask_ExecuteRelationshipQueryCompleted, completed, wait for resultsLayer_Initialized"));
		} // private void QueryTask_ExecuteRelationshipQueryCompleted(object sender, RelationshipEventArgs e)


		private void resultsLayer_Initialized(object sender, EventArgs e) {
			try {
				doResultsLayer_Initialized(sender, e);
			}
			catch(Exception ex) {
				log(string.Format("resultsLayer_Initialized, error {0}", ex.Message));
				MessageBox.Show(string.Format("Не удалось отобразить связанные записи \n {0}", ex.Message));
				ClosePopup();
			}
		} // private void resultsLayer_Initialized(object sender, EventArgs e)


		/// <summary>
		/// Get related records ID's from query result, add filtered by ID's layer to map
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void doResultsLayer_Initialized(object sender, EventArgs e) {
			// Get the FeatureLayer's OID field off the LayerInfo
			string oidField = resultsLayer.getFL().LayerInfo.ObjectIdField;
			var reslyr = resultsLayer;
			string oidFieldAlias = reslyr.getFieldAlias(oidField);
			log(string.Format("doResultsLayer_Initialized, resultsLayer.oidfield='{0}', alias='{1}'", oidField, oidFieldAlias));
			oidField = "";

			// Create a List to hold the ObjectIds
			List<int> list = new List<int>();
			IEnumerable<Graphic> RelatedRecords;

			//Go through the RelatedRecordsGroup and add the Graphic to the IEnumerable<Graphic> 
			foreach(var records in queryResult.RelatedRecordsGroup) {
				RelatedRecords = records.Value;
				foreach(Graphic graphic in RelatedRecords) {
					if(oidField == "") {
						var oid = resultsLayer.getOID(graphic);
						oidField = resultsLayer.getOIDFieldnameOrAlias();
						log(string.Format("doResultsLayer_Initialized, real resultsLayer.oidfield='{0}', alias='{1}'", oidField, oidFieldAlias));
					}
					list.Add((int)graphic.Attributes[oidField]);
				}
			}
			log(string.Format("doResultsLayer_Initialized, relatedRecords.oidList.Count='{0}'", list.Count));
			if(list.Count <= 0) {
				throw new Exception("Для указанного обьекта связанные записи отсутствуют");
			}

			resultsLayer.getFL().UpdateCompleted += resultsLayer_UpdateCompleted;
			int[] objectIDs = list.ToArray();
			resultsLayer.getFL().ObjectIDs = objectIDs;
			log(string.Format("doResultsLayer_Initialized, ID's set"));

			// Specify renderers for Point, Polyline, and Polygon features if the related features have geometry.
			if(resultsLayer.getFL().LayerInfo.GeometryType == GeometryType.Point) {
				log(string.Format("doResultsLayer_Initialized, MapPoint"));
				resultsLayer.getFL().Renderer = new SimpleRenderer() {
					Symbol = new SimpleMarkerSymbol() {
						Style = SimpleMarkerSymbol.SimpleMarkerStyle.Circle
					}
				};
			}
			else if(resultsLayer.getFL().LayerInfo.GeometryType == GeometryType.Polyline) {
				log(string.Format("doResultsLayer_Initialized, Polyline"));
				resultsLayer.getFL().Renderer = new SimpleRenderer() {
					Symbol = new SimpleLineSymbol() {
						Color = new SolidColorBrush(Colors.Red),
						Width = 2
					}
				};
			}
			else if(resultsLayer.getFL().LayerInfo.GeometryType == GeometryType.Polygon) {
				log(string.Format("doResultsLayer_Initialized, Polygon"));
				resultsLayer.getFL().Renderer = new SimpleRenderer() {
					Symbol = new SimpleFillSymbol() {
						Fill = new SolidColorBrush(Color.FromArgb(125, 255, 0, 0)),
						BorderBrush = new SolidColorBrush(Colors.Red)
					}
				};
			}
			log(string.Format("doResultsLayer_Initialized, resultsLayer.Geometry is '{0}'", resultsLayer.getFL().LayerInfo.GeometryType));

			// Specify a layer name so that it displays on the Attribute table, but do not display the layer in the Map Contents.
			// old style
			string mapLayerName = resultsLayer.getFL().LayerInfo.Name + ", related records '" + relationInfo.name +
				"' for OID " + relatesLayer.getOID(inputFeature).ToString();
			try { // Parilov style
				mapLayerName = string.Format("{0}, {1}", resultsLayer.getFL().LayerInfo.Name,
					inputFeature.Attributes[relatesLayer.getFL().LayerInfo.DisplayField]);
			}
			catch(Exception ex) {
				log(string.Format("doResultsLayer_Initialized, Parilov style layer name failed. SetLayerName '{0}'", mapLayerName));
			}
			MapApplication.SetLayerName(resultsLayer.lyr, mapLayerName);
			LayerProperties.SetIsVisibleInMapContents(resultsLayer.lyr, true);
			log(string.Format("doResultsLayer_Initialized, SetLayerName '{0}'", mapLayerName));
			// Add the layer to the map and set it as the selected layer so the attributes appear in the Attribute table.
			MapApplication.Current.Map.Layers.Add(resultsLayer.lyr);
		} // private void doResultsLayer_Initialized(object sender, EventArgs e)


		/// <summary>
		/// Raised when the results layer is updated. Dispalys the attribute table and closes the pop-up window.
		/// </summary>
		private void resultsLayer_UpdateCompleted(object sender, EventArgs e) {
			// Display the Attribute table and close the pop-up.
			//MapApplication.Current.SelectedLayer = resultsLayer;
			ShowFeatureDataGrid();
			ClosePopup();
			MapApplication.Current.SelectedLayer = resultsLayer.lyr;
		}

		/// <summary>
		/// Handles failed QueryTask by displaying an error message. 
		/// </summary>
		private void QueryTask_Failed(object sender, TaskFailedEventArgs e) {
			// Show failure error message
			TextBlock failureText = new TextBlock() {
				Margin = new Thickness(10),
				Text = e.Error.Message
			};
			MapApplication.Current.ShowWindow("Error", failureText, true);
		}


		#endregion


		#region Private Methods


		/// <summary>
		/// found related records for each relation for selected feature;
		/// -1 means unknown number of related records
		/// </summary>
		/// <param name="vfl"></param>
		/// <returns></returns>
		private int countRelatedRecords(mwb02.AddIns.VLayer vfl, ESRI.ArcGIS.Client.Graphic feature) {
			int allLinkedRecordsCount = -1; // unknown number

			if(this.isBusy) { // wait for previous request completion
				//log(string.Format("countRelatedRecords.isBusy; lyr {0}", vfl.lyrUrl));
				return allLinkedRecordsCount;
			}

			// get layer from storage
			mwb02.AddIns.VLayer storedLyr = null;
			if(!this.flList.TryGetValue(vfl.lyrUrl, out storedLyr)) {
				log(string.Format("countRelatedRecords, not in storage, lyr {0}", vfl.lyrUrl));
				storedLyr = vfl;
				this.flList.Add(vfl.lyrUrl, storedLyr);
			}
			vfl = storedLyr;

			// Get the name of the ObjectID field.
			var oidFieldname = vfl.getOIDFieldnameOrAlias();
			// get key value
			int objIdValue = vfl.getOID(feature);
			if(vfl.relations == null) {
				vfl.initRelations();
			}
			//log(string.Format("countRelatedRecords, lyr {0}, keyField {1}, key {2}, rels.count {3}", vfl.lyrUrl, oidFieldname, objIdValue, vfl.relations.Count));

			// count rel.records using stored info
			allLinkedRecordsCount = 0;
			foreach(var rel in vfl.relations) {
				if(rel.linkedRecords == null) {
					allLinkedRecordsCount = -1;
					log(string.Format("countRelatedRecords, rel.linkedRecords=null, lyr {0}, key {1}, rel {2}", vfl.lyrUrl, objIdValue, rel.name));
					break; // need get data from server
				}
				else {
					IEnumerable<Graphic> recs; // feature related records
					if(rel.linkedRecords.TryGetValue(objIdValue, out recs)) {
						//log(string.Format("countRelatedRecords, have stored data for feature {0}/{1}, rel {2}, relrecs {3}",vfl.lyrUrl, objIdValue, rel.name, recs==null?0:recs.Count()));
						if(recs != null) allLinkedRecordsCount += recs.Count();
					}
					else {
						allLinkedRecordsCount = -1;
						log(string.Format("countRelatedRecords, no stored data for feature {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, rel.name));
						break; // need get data from server
					}
				}
			}
			// stored info exists for this layer and this feature
			if(allLinkedRecordsCount != -1) {
				//log(string.Format("countRelatedRecords, have count from stored data, {0}/{1}, allrecs {2}", vfl.lyrUrl, objIdValue, allLinkedRecordsCount));
				return allLinkedRecordsCount;
			}

			// otherwise we should get data from server
			log(string.Format("countRelatedRecords, ask server for relrecs, {0}/{1}", vfl.lyrUrl, objIdValue));
			askServerForRelatedRecs(vfl, feature);
			log(string.Format("countRelatedRecords, wait for answer, {0}/{1}", vfl.lyrUrl, objIdValue));
			return allLinkedRecordsCount;
		} // countRelatedRecords


		/// <summary>
		/// ask related records from server for each relatioin, one relation at a time
		/// </summary>
		/// <param name="vfl"></param>
		/// <param name="feature"></param>
		private void askServerForRelatedRecs(mwb02.AddIns.VLayer vfl, ESRI.ArcGIS.Client.Graphic feature) {
			var fl = vfl.lyr as FeatureLayer;
			var rels = fl.LayerInfo.Relationships;
			// Get the name of the ObjectID field.
			var oidFieldname = vfl.getOIDFieldnameOrAlias();
			// get key value
			int objIdValue = vfl.getOID(feature);
			log(string.Format("askServerForRelatedRecs, ready for ask, {0}/{1}", vfl.lyrUrl, objIdValue));

			//for each relation
			if(vfl.relations == null) vfl.initRelations();
			foreach(var rel in rels) {
				var relinfo = vfl.getRelation(rel);
				if(relinfo == null) {
					throw new Exception(string.Format("askServerForRelatedRecs, stored relinfo is null, make it; {0}/{1}, rel {2}",
						vfl.lyrUrl, objIdValue, rel.Name));
				}
				log(string.Format("askServerForRelatedRecs, have relinfo {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));

				if(relinfo.linkedRecords == null) {
					log(string.Format("askServerForRelatedRecs, create linkedRecords dict {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));
					relinfo.linkedRecords = new Dictionary<int, IEnumerable<Graphic>>();
				}
				IEnumerable<Graphic> recs;
				if(relinfo.linkedRecords.TryGetValue(objIdValue, out recs)) {
					log(string.Format("askServerForRelatedRecs, have relrecs for feature already, skip; {0}/{1}, rel {2}, count {3}",
						vfl.lyrUrl, objIdValue, relinfo.name, recs == null ? 0 : recs.Count()));
					continue; // goto next relation
				}

				log(string.Format("askServerForRelatedRecs, haven't relrecs for feature, get it! {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));
				// get data from server. then store it
				var qt = new QueryTask();

				// callbacks
				qt.Failed += (sender, eventargs) => {
					this.isBusy = false;
					log(string.Format("askServerForRelatedRecs, QueryTask.Failed! {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));
					relinfo.linkedRecords.Add(objIdValue, null);
					if(CanExecuteChanged != null) { CanExecuteChanged(this, EventArgs.Empty); }
				}; // qt.failed

				qt.ExecuteRelationshipQueryCompleted += (sender, eventargs) => {
					this.isBusy = false;
					log(string.Format("askServerForRelatedRecs, QueryTask.Completed, {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));
					var qres = eventargs.Result;
					recs = null;
					foreach(var records in qres.RelatedRecordsGroup) {
						var graphics = records.Value;
						if(recs == null) recs = graphics;
						else recs = recs.Concat(graphics);
					}
					log(string.Format("askServerForRelatedRecs, QueryTask.Completed, {0}/{1}, rel {2}, recs {3}",
						vfl.lyrUrl, objIdValue, relinfo.name, recs == null ? 0 : recs.Count()));
					// store data, repeat for next relation
					relinfo.linkedRecords.Add(objIdValue, recs);
					if(CanExecuteChanged != null) { CanExecuteChanged(this, EventArgs.Empty); }
				}; // qt.ExecuteRelationshipQueryCompleted

				// Input parameters for QueryTask
				if(!qt.IsBusy) {
					RelationshipParameter relationshipParameters = new RelationshipParameter() {
						ObjectIds = new int[] { objIdValue },
						OutFields = new string[] { "*" }, // Return all fields
						ReturnGeometry = true, // Return the geometry so that features can be displayed on the map if applicable
						RelationshipId = relinfo.id, // Obtain the desired RelationshipID from the Service Details page. Here it takes the first relationship it finds if there is more than one.
						OutSpatialReference = MapApplication.Current.Map.SpatialReference
					};
					qt.Url = fl.Url;
					this.isBusy = true;
					log(string.Format("askServerForRelatedRecs, QueryTask.Execute, {0}/{1}, rel {2}", vfl.lyrUrl, objIdValue, relinfo.name));

					qt.ExecuteRelationshipQueryAsync(relationshipParameters);
					return;
				}
			} // for each relation
		} // private void askServerForRelatedRecs(mwb02.AddIns.VLayer vfl, ESRI.ArcGIS.Client.Graphic feature)


		/// <summary>
		/// Return FeatureLayer from ArcGISDynamicMapServiceLayer by lyrID
		/// </summary>
		/// <param name="lyr"></param>
		/// <param name="lyrID"></param>
		/// <returns></returns>
		private Layer getSubLayer(mwb02.AddIns.VLayer lyr, int lyrID) {
			var fl = lyr.getSubLayer(lyrID);
			if(fl == null)
				throw new Exception("Не удалось вынуть FeatureLayer из ArcGISDynamicMapServiceLayer");
			return fl.lyr;
		} // private Layer getSubLayer(mwb02.AddIns.VLayer lyr, int lyrID)


		/// <summary>
		/// Displays the attribute table.
		/// </summary>
		private void ShowFeatureDataGrid() {
			// Get the attribute table container
			FrameworkElement container = MapApplication.Current.FindObjectInLayout(CONTAINER_NAME) as FrameworkElement;
			if(container != null) {
				// try to get storyboard (animation) for showing attribute table
				Storyboard showStoryboard = container.FindStoryboard(CONTAINER_NAME + "_Show");
				if(showStoryboard != null)
					showStoryboard.Begin(); // use storyboard if available
				else
					container.Visibility = Visibility.Visible; // no storyboard, so set visibility directly
			}
		}

		/// <summary>
		/// Closes the Pop-up window.
		/// </summary>
		private void ClosePopup() {
			// Remove the indicator from the pop-up so it doesn't display the next
			// time the pop-up is opened.
			attributeGrid.Children.Remove(indicator);

			// Close the pop-up window
			InfoWindow popupWindow = popupInfo.Container as InfoWindow;
			//popupWindow.IsOpen = false;
		}

		#endregion

		public void log(String txt) {
			//var vers = Assembly.GetExecutingAssembly().GetName().Version;
			var vers = "20130219";
			DateTime dt = DateTime.Now;
			var msg = string.Format("{0} QueryRelatedTool.{2} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt, vers);
			//var msg = string.Format("{0} QueryRelatedTool {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

	} // public class QueryRelatedTool : ICommand


	/// <summary>
	/// log to browser console (ie8 script console or FF firebug)
	/// </summary>
	public static class VExtClass { // http://kodierer.blogspot.com/2009/05/silverlight-logging-extension-method.html
		/// <summary>
		/// if you are using Firefox with the Firebug add-on or
		/// Internet Explorer 8: Use the console.log mechanism
		/// </summary>
		/// <param name="obj"></param>
		public static void clog(this object obj) {
			mwb02.AddIns.VExtClass.clog(obj);
		} // public static void clog(this object obj)

	} // public static class VExtClass

} // namespace QueryRelatedRecords.AddIns
