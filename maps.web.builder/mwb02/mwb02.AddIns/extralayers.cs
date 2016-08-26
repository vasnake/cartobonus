using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Interactivity;
//using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Browser;

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.IO.IsolatedStorage;
using System.Json;
using System.Windows.Threading;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Actions;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Tasks;


namespace mwb02.AddIns {
	[Export(typeof(ICommand))]
	[DisplayName("Доп. слои")]
	[Description("Добавить слои из нестандартных источников")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/extralayers.png")]
	public class VExtraLayersCommand: ICommand, ISupportsConfiguration {
		/* c:\Inetpub\wwwroot\Apps\app5\Config\Tools.xml
			  <Tool Label="Доп. слои" Icon="/mwb02.AddIns;component/Images/extralayers.png" Description="Добавить слои из нестандартных источников">
				<Tool.Class>
				  <VExtraLayersCommand xmlns="clr-namespace:mwb02.AddIns;assembly=mwb02.AddIns" />
				</Tool.Class>
				<Tool.ConfigData>extralayers.config.xml</Tool.ConfigData>
			  </Tool>
		 */

		/// <summary>
		/// service endpoint, configDialog.InputTextBox.Text is like
		/// extralayers.config.xml
		/// </summary>
		private MyConfigDialog configDialog = new MyConfigDialog();

		/// <summary>
		/// ICommand.Execute implementation
		/// </summary>
		public VExtraLayersImpl extralayers = new VExtraLayersImpl();

		/// <summary>
		/// constructor
		/// </summary>
		public VExtraLayersCommand() {
			extralayers = new VExtraLayersImpl(this.log);
		}


		#region ISupportsConfiguration members

		public void Configure() {
			// When the dialog opens, it shows the information saved from the last configuration
			MapApplication.Current.ShowWindow("Configuration", configDialog);
		} // public void Configure()

		public void LoadConfiguration(string configData) {
			// Initialize the behavior's configuration with the saved configuration data.
			// The dialog's textbox is used to store the configuration.
			configDialog.InputTextBox.Text = configData;
		} // public void LoadConfiguration(string configData)

		public string SaveConfiguration() {
			// Save the information from the configuration dialog
			return configDialog.InputTextBox.Text;
		} // public string SaveConfiguration()

		#endregion // #region ISupportsConfiguration members


		#region ICommand members

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter) {
			try {
				if(MapApplication.Current.Map != null) {
					return true;
				}
				return false;
			}
			catch(Exception ex) {
				log(string.Format("CanExecute, error {0}, {1}", ex.Message, ex.StackTrace));
				return false;
			}
		} // public bool CanExecute(object parameter)

		public void Execute(object parameter) {
			log("Execute, ...");
			try {
				extralayers.runTool(SaveConfiguration());
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой вызова инструмента 'Доп.слои': \n {0}", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // public void Execute(object parameter)

		#endregion // #region ICommand members


		public void log(String txt) {
			var vers = "20130307";
			DateTime dt = DateTime.Now;
			var msg = string.Format("{0} Extralayers.{2} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt, vers);
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

	} // public class VExtraLayersCommand: ICommand, ISupportsConfiguration


	public class VExtraLayersImpl: VAddonCommand {

		private string proxyUrl = "http://servicesbeta3.esri.com/SilverlightDemos/ProxyPage/proxy.ashx";
		public VExtraLayersForm layersTypesForm;
		public VWMSParametersForm wmsParamsForm;
		public VKMLParametersForm kmlParamsForm;
		public VWMTSParametersForm wmtsParamsForm;
		public VCSVParametersForm csvParamsForm;
		public VJSONParametersForm jsonParamsForm;

		public void nullLog(string msg) { msg.clog(); }

		public VExtraLayersImpl(logFunc l) {
			log = l;
			configure();
		}

		public VExtraLayersImpl() {
			log = nullLog;
			configure();
		}

		private void configure() {
			// call from constructor
			log(string.Format("VExtraLayersImpl.configure..."));

			if(log == nullLog) { return; }

			// map events, timer, etc.
			try {
				log(string.Format("VExtraLayersImpl.configure, SnapToLevels {0}", MapApplication.Current.Map.SnapToLevels));
				// init app staff
				layersTypesForm = new VExtraLayersForm() { app = this };
				var lbitems = layersTypesForm.listBox1.Items;
				lbitems.Clear();
				lbitems.Add("WMS (Web Map Service)");
				lbitems.Add("WMTS (Web Map Tiled Service)");
				lbitems.Add("KML/KMZ (Keyhole Markup Language)");
				lbitems.Add("CSV (Comma Separated Values)");
				lbitems.Add("JSON (JavaScript Object Notation)");
				
				wmsParamsForm = new VWMSParametersForm() { app = this };
				wmtsParamsForm = new VWMTSParametersForm() { app = this };
				kmlParamsForm = new VKMLParametersForm() { app = this };
				csvParamsForm = new VCSVParametersForm() { app = this };
				jsonParamsForm = new VJSONParametersForm() { app = this };
			}
			catch(Exception ex) {
				log(string.Format("VExtraLayersImpl.configure, Initialization failed: {0}", ex.Message));
			}
		} // public void configure()


		/// <summary>
		/// call from ICommand.Execute
		/// </summary>
		public void runTool(string configUrl) {
			log("runTool ...");

			// get config file from server
			WebClient wc = new WebClient();
			Uri uri = new Uri(configUrl, UriKind.RelativeOrAbsolute);
			wc.OpenReadCompleted += (sender, args) => {
				if(args.Error != null) {
					log(String.Format("configUrl wc_OpenReadCompleted, error in reading answer, msg [{0}], trace [{1}]",
						args.Error.Message, args.Error.StackTrace));
				}
				else {
					try {
						StreamReader reader = new StreamReader(args.Result);
						string text = reader.ReadToEnd();
						log(string.Format("configUrl wc_OpenReadCompleted, resp '{0}'", text));
						var jo = JsonObject.Parse(text);
						if(jo.ContainsKey("proxyUrl")) {
							proxyUrl = jo["proxyUrl"];
						}
					}
					catch(Exception ex) {
						log(String.Format("configUrl wc_OpenReadCompleted, parsing failed {0}, {1}", ex.Message, ex.StackTrace));
					}
				}
			}; // wc.OpenReadCompleted

			wc.OpenReadAsync(uri);

			MapApplication.Current.ShowWindow("Добавление нестандартных слоев",
				layersTypesForm, false, evtBeforeHideLayersTypesForm, evtAfterHideLayersTypesForm, WindowType.Floating);
		} // public void runTool()


		/// <summary>
		/// Call from form.
		/// Close form, abort procedure
		/// </summary>
		public void cancelAdding() {
			MapApplication.Current.HideWindow(layersTypesForm);
		} // public void cancelAdding()


		/// <summary>
		/// Call from form.
		/// Open LayerParametersForm for selected layer type
		/// </summary>
		/// <param name="lyrtype"></param>
		public void userPickLayerType(string lyrtype) {
			log(string.Format("userPickLayerType, layer type '{0}'", lyrtype));
			MapApplication.Current.HideWindow(layersTypesForm);

			if(lyrtype.Contains("WMS")) {
				log(string.Format("userPickLayerType, start adding WMS layer..."));
				if(wmsParamsForm.ProxyTextBox.Text.Length < 11) {
					wmsParamsForm.ProxyTextBox.Text = this.proxyUrl;
				}
				MapApplication.Current.ShowWindow("Параметры WMS слоя",
					wmsParamsForm, false, null, null, WindowType.Floating);
			}

			else if(lyrtype.Contains("WMTS")) {
				log(string.Format("userPickLayerType, start adding WMTS layer..."));
				if(wmtsParamsForm.ProxyTextBox.Text.Length < 11) {
					wmtsParamsForm.ProxyTextBox.Text = this.proxyUrl;
				}
				MapApplication.Current.ShowWindow("Параметры WMTS слоя",
					wmtsParamsForm, false, null, null, WindowType.Floating);
			}

			else if(lyrtype.Contains("KML/KMZ")) {
				log(string.Format("userPickLayerType, start adding KML layer..."));
				if(kmlParamsForm.ProxyTextBox.Text.Length < 11) {
					kmlParamsForm.ProxyTextBox.Text = this.proxyUrl;
				}
				MapApplication.Current.ShowWindow("Параметры KML слоя",
					kmlParamsForm, false, null, null, WindowType.Floating);
			}

			else if(lyrtype.Contains("CSV")) {
				log(string.Format("userPickLayerType, start adding CSV layer..."));
				if(csvParamsForm.ProxyTextBox.Text.Length < 11) {
					csvParamsForm.ProxyTextBox.Text = this.proxyUrl;
				}
				MapApplication.Current.ShowWindow("Параметры CSV слоя",
					csvParamsForm, false, null, null, WindowType.Floating);
			}

			else if(lyrtype.Contains("JSON")) {
				log(string.Format("userPickLayerType, start adding JSON layer..."));
				if(jsonParamsForm.ProxyTextBox.Text.Length < 11) {
					jsonParamsForm.ProxyTextBox.Text = this.proxyUrl;
				}
				MapApplication.Current.ShowWindow("Параметры JSON слоя",
					jsonParamsForm, false, null, null, WindowType.Floating);
			}

			else {
				log(string.Format("userPickLayerType, unknown layer type, WTF?"));
			}
		} // public void userPickLayerType(string lyrtype)


		/// <summary>
		/// Call from form.
		/// Add WMS layer to map.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="wmslayers"></param>
		/// <param name="version"></param>
		/// <param name="lyrname"></param>
		/// <param name="proxy"></param>
		public void userGiveWMSLayerParams(string url, string wmslayers, string version, string lyrname, string proxy) {
			log(string.Format("userGiveWMSLayerParams, {3}, url '{0}', layers '{1}', version '{2}'", url, wmslayers, version, lyrname));
			MapApplication.Current.HideWindow(wmsParamsForm);

			var lyr = new ESRI.ArcGIS.Client.Toolkit.DataSources.WmsLayer();
			lyr.Url = url;
			//lyr.ProxyUrl = "http://servicesbeta3.esri.com/SilverlightDemos/ProxyPage/proxy.ashx";
			//lyr.ProxyUrl = "http://vdesk.algis.com/agsproxy/proxy.ashx";
			//lyr.ProxyUrl = this.proxyUrl;
			lyr.ProxyUrl = proxy;
			lyr.SkipGetCapabilities = false;
			lyr.Version = string.IsNullOrEmpty(version) ? "1.1.1" : version;
			lyr.Opacity = 1;
			lyr.Layers = string.IsNullOrEmpty(wmslayers) ? null : wmslayers.Split(',');
			lyr.ID = VExtClass.computeSHA1Hash(string.Format("{0}, {1}, {2}", url, wmslayers, version));

			lyr.Initialized += (osender, eargs) => {
				log(string.Format("userGiveWMSLayerParams.Initialized, turn on all sublayers for {0}-{1}", lyrname, lyr.ID));
				List<string> layerNames = new List<string>();
				foreach(var layerInfo in lyr.LayerList) {
					if(layerInfo.Name != null) {
						layerNames.Add(layerInfo.Name);
					}
				}
				lyr.Layers = layerNames.ToArray();
			};
			lyr.Initialize();

			MapApplication.SetLayerName(lyr, lyrname);
			ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsVisibleInMapContents(lyr, true);
			MapApplication.Current.Map.Layers.Add(lyr);
			log(string.Format("userGiveWMSLayerParams, done for {3}, url '{0}', layers '{1}', version '{2}'", url, wmslayers, version, lyrname));
		} // public void userGiveWMSLayerParams(string url, string wmslayers, string version, string lyrname)


		/// <summary>
		/// Call from form, add WMTS layer to map
		/// </summary>
		/// <param name="url"></param>
		/// <param name="lyrname"></param>
		/// <param name="proxy"></param>
		/// <param name="sublyr"></param>
		public void userGiveWMTSLayerParams(string url, string lyrname, string proxy, string sublyr) {
			log(string.Format("userGiveWMTSLayerParams, url '{0}', proxy '{1}', name '{2}'", url, proxy, lyrname));
			MapApplication.Current.HideWindow(wmtsParamsForm);

			var lyr = new ESRI.ArcGIS.Client.Toolkit.DataSources.WmtsLayer();
			//lyr.ServiceMode = ESRI.ArcGIS.Client.Toolkit.DataSources.WmtsLayer.WmtsServiceMode.KVP;
			lyr.Url = url; // http://v2.suite.opengeo.org/geoserver/gwc/service/wmts?service=WMTS&request=GetCapabilities&version=1.0.0
			lyr.ProxyUrl = proxy;
			lyr.Layer = string.IsNullOrEmpty(sublyr) ? null : sublyr; // "world:cities";
			lyr.Opacity = 1;
			lyr.ID = VExtClass.computeSHA1Hash(string.Format("{0}-{1}", url, sublyr));

			lyr.Initialized += (osender, eargs) => {
				log(string.Format("userGiveWMTSLayerParams.Initialized, turn on all sublayers for {0}-{1}", lyrname, lyr.ID));
			};
			lyr.Initialize();

			MapApplication.SetLayerName(lyr, lyrname);
			ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsVisibleInMapContents(lyr, true);
			MapApplication.Current.Map.Layers.Add(lyr);
			log(string.Format("userGiveWMTSLayerParams, done for url '{0}', proxy '{1}', name '{2}'", url, proxy, lyrname));
		} // public void userGiveWMTSLayerParams(string url, string lyrname, string proxy, string sublyr)


		/// <summary>
		/// Call from form, add KML layer to map.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="layers"></param>
		/// <param name="lyrname"></param>
		/// <param name="proxy"></param>
		public void userGiveKMLLayerParams(string url, string layers, string lyrname, string proxy) {
			log(string.Format("userGiveKMLLayerParams, name '{3}', url '{0}', layers '{1}', proxy '{2}'", url, layers, proxy, lyrname));
			MapApplication.Current.HideWindow(kmlParamsForm);

			var lyr = new ESRI.ArcGIS.Client.Toolkit.DataSources.KmlLayer();
			lyr.Url = new System.Uri(url, UriKind.RelativeOrAbsolute);
			lyr.ProxyUrl = proxy;
			lyr.VisibleLayers = string.IsNullOrEmpty(layers) ? null : layers.Split(',');
			lyr.Opacity = 1;
			lyr.ID = VExtClass.computeSHA1Hash(string.Format("{0}, {1}", url, layers));

			lyr.Initialized += (osender, eargs) => {
				log(string.Format("userGiveKMLLayerParams.Initialized, {0}-{1}", lyrname, lyr.ID));
			};
			lyr.Initialize();

			MapApplication.SetLayerName(lyr, lyrname);
			ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsVisibleInMapContents(lyr, true);
			MapApplication.Current.Map.Layers.Add(lyr);
			log(string.Format("userGiveKMLLayerParams, done for {3}, url '{0}', layers '{1}', proxy '{2}'", url, layers, proxy, lyrname));
		} // public void userGiveKMLLayerParams(string url, string layers, string lyrname, string proxy)


		/// <summary>
		/// Call from form, add CSV layer to map
		/// </summary>
		/// <param name="url"></param>
		/// <param name="lyrname"></param>
		/// <param name="proxy"></param>
		public void userGiveCSVLayerParams(string url, string lyrname, string proxy) {
			log(string.Format("userGiveCSVLayerParams, name '{2}', url '{0}', proxy '{1}'", url, proxy, lyrname));
			MapApplication.Current.HideWindow(csvParamsForm);

			var lyr = new ESRI.ArcGIS.Client.Toolkit.DataSources.CsvLayer();
			lyr.Url = url;
			lyr.ProxyUrl = proxy;
			lyr.Opacity = 1;
			lyr.ID = VExtClass.computeSHA1Hash(string.Format("{0}", url));

			lyr.Initialized += (osender, eargs) => {
				log(string.Format("userGiveCSVLayerParams.Initialized, {0}-{1}", lyrname, lyr.ID));
			};
			lyr.Initialize();

			MapApplication.SetLayerName(lyr, lyrname);
			ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsVisibleInMapContents(lyr, true);
			MapApplication.Current.Map.Layers.Add(lyr);
			log(string.Format("userGiveCSVLayerParams, done for name '{2}', url '{0}', proxy '{1}'", url, proxy, lyrname));
		} // public void userGiveCSVLayerParams(string url, string lyrname, string proxy)


		/// <summary>
		/// Call from form, add to map graphicslayer from json
		/// </summary>
		/// <param name="url"></param>
		/// <param name="lyrname"></param>
		/// <param name="proxy"></param>
		public void userGiveJSONLayerParams(string url, string lyrname, string proxy) {
			log(string.Format("userGiveJSONLayerParams, name '{2}', url '{0}', proxy '{1}'", url, proxy, lyrname));
			MapApplication.Current.HideWindow(jsonParamsForm);

			var requrl = string.IsNullOrEmpty(proxy) ? url : string.Format("{0}?{1}", proxy, url);

			// get json text
			WebClient wc = new WebClient();
			Uri uri = new Uri(requrl, UriKind.RelativeOrAbsolute);
			wc.OpenReadCompleted += (sender, args) => {
				if(args.Error != null) {
					log(String.Format("userGiveJSONLayerParams wc_OpenReadCompleted, error in reading answer, msg [{0}], trace [{1}]",
						args.Error.Message, args.Error.StackTrace));
				}
				else {
					try {
						StreamReader reader = new StreamReader(args.Result);
						string text = reader.ReadToEnd();
						log(string.Format("userGiveJSONLayerParams wc_OpenReadCompleted, resp '{0}'", text));

						// got layer content, make layer
						var featureSet = FeatureSet.FromJson(text);
						var graphicsLayer = new GraphicsLayer() {
							Graphics = new GraphicCollection(featureSet)
						};
						// set layer params
						graphicsLayer.Opacity = 1;
						graphicsLayer.ID = VExtClass.computeSHA1Hash(string.Format("{0}", text));
						graphicsLayer.Initialized += (osender, eargs) => {
							log(string.Format("userGiveJSONLayerParams.Initialized, {0}-{1}", lyrname, graphicsLayer.ID));
						};

						// projection
						var MyMap = MapApplication.Current.Map;
						var mercator = new ESRI.ArcGIS.Client.Projection.WebMercator();
						if(!featureSet.SpatialReference.Equals(MyMap.SpatialReference)) {
							if(MyMap.SpatialReference.Equals(new SpatialReference(102100)) &&
								featureSet.SpatialReference.Equals(new SpatialReference(4326)))
								foreach(Graphic g in graphicsLayer.Graphics)
									g.Geometry = mercator.FromGeographic(g.Geometry);

							else if(MyMap.SpatialReference.Equals(new SpatialReference(4326)) &&
								featureSet.SpatialReference.Equals(new SpatialReference(102100)))
								foreach(Graphic g in graphicsLayer.Graphics)
									g.Geometry = mercator.ToGeographic(g.Geometry);

							else {
								var geometryService = new GeometryService(
									"http://tasks.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer");

								geometryService.ProjectCompleted += (s, a) => {
									for(int i = 0; i < a.Results.Count; i++)
										graphicsLayer.Graphics[i].Geometry = a.Results[i].Geometry;
								};

								geometryService.Failed += (s, a) => {
									MessageBox.Show("Ошибка проецирования: " + a.Error.Message);
								};

								geometryService.ProjectAsync(graphicsLayer.Graphics, MyMap.SpatialReference);
							}
						} // if map.SR != featureset.SR

						// add layer to map
						graphicsLayer.Initialize();
						MapApplication.SetLayerName(graphicsLayer, lyrname);
						ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsVisibleInMapContents(graphicsLayer, true);
						MapApplication.Current.Map.Layers.Add(graphicsLayer);
					} // got json text
					catch(Exception ex) {
						log(String.Format("userGiveJSONLayerParams wc_OpenReadCompleted, make layer failed {0}, {1}", ex.Message, ex.StackTrace));
					}
				}
			}; // wc.OpenReadCompleted
			wc.OpenReadAsync(uri);

			log(string.Format("userGiveJSONLayerParams, done for name '{2}', url '{0}', proxy '{1}'", url, proxy, lyrname));
		} // public void userGiveJSONLayerParams(string url, string lyrname, string proxy)


		/// <summary>
		/// Call from Map when HideWindow sequence started.
		/// Maybe you want cancel hiding window?
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void evtBeforeHideLayersTypesForm(object sender, System.ComponentModel.CancelEventArgs args) {
			log("evtBeforeHideLayersTypesForm");
			try {
				//args.Cancel = true;
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtBeforeHideLayersTypesForm(object sender, CancelEventArgs args)


		/// <summary>
		/// Call from Map when window hided.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void evtAfterHideLayersTypesForm(object sender, EventArgs args) {
			log("evtAfterHideLayersTypesForm");
			try {
				// ?
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtAfterHideLayersTypesForm(object sender, EventArgs args)

	} // public class VExtraLayersImpl: VAddonCommand 

}
