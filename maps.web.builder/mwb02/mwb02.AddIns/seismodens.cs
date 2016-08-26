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
	/// <summary>
	/// User draw a polygon shape, send request to service and show response to user.
	/// Response contains a seismodensity of area.
	/// </summary>
	[Export(typeof(ICommand))]
	[DisplayName("Seismodens")]
	[Description("Сейсмоплотность")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/seismodens.png")]
	public class VSeismodensCommand: ICommand, ISupportsConfiguration {

		/// <summary>
		/// service endpoint, configDialog.InputTextBox.Text is like
		/// http://site/rngis/mapsupport/seismicdensity?calcsrid=32&amp;mapsrid=32
		/// </summary>
		private MyConfigDialog configDialog = new MyConfigDialog();

		/// <summary>
		/// ICommand.Execute implementation
		/// </summary>
		public VSeismodensImpl vSD = new VSeismodensImpl();

		/// <summary>
		/// constructor
		/// </summary>
		public VSeismodensCommand() {
			vSD = new VSeismodensImpl(this.log);
		} // public VSeismodensCommand()


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
				//log(string.Format("CanExecute, param '{0}'", parameter==null?"null":parameter.GetType().ToString()));
				// todo: get current layer: check GP service availability
				var currentLayer = new VLayer();
				bool seismoGeoprocessorOK = false;
				if(
					currentLayer.lyrUrl.Contains("seismoprofiles") &&
					seismoGeoprocessorOK &&
					MapApplication.Current.Map != null
				) { return true; }
				return false;
			}
			catch(Exception ex) {
				log(string.Format("CanExecute, error {0}, {1}", ex.Message, ex.StackTrace));
				return false;
			}
			// todo: seismoprofiles layer must be exist
		} // public bool CanExecute(object parameter)

		public void Execute(object parameter) {
			log("Execute, ...");
			try {
				log(string.Format("Execute, param '{0}'", parameter == null ? "null" : parameter.GetType().ToString()));
				vSD.baseUrl = this.SaveConfiguration();
				vSD.runTool(parameter);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой вызова инструмента 'Сейсмоплотность': \n {0}", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // public void Execute(object parameter)

		#endregion // #region ICommand members


		public void log(String txt) {
			var vers = "20120928";
			DateTime dt = DateTime.Now;
			var msg = string.Format("{0} Seismodens.{2} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt, vers);
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

	} // public class VSeismodensCommand: ICommand, ISupportsConfiguration


	public class VSeismodensImpl: VAddonCommand {

		public static string layerID = "seismodensRL";
		public static string layerName = "Сейсмоплотность.RL";
		public Draw draw;
		public VPseudoWindow frmElement;
		public static ESRI.ArcGIS.Client.Geometry.Geometry currGeom;
		public ESRI.ArcGIS.Client.Symbols.SimpleFillSymbol polySymbol, drawSymbol;
		public RLAttribsForm rlForm; // symbols
		public string baseUrl = ""; // http://vdesk.algis.com:8088/rngis/mapsupport/seismicdensity?calcsrid=32&mapsrid=32

		public void nullLog(string msg) { msg.clog(); }

		public VSeismodensImpl(logFunc l) {
			log = l;
			configure();
		}

		public VSeismodensImpl() {
			log = nullLog;
			configure();
		}

		private void configure() {
			// call from constructor
			log(string.Format("VSeismodensImpl.configure..."));

			if(log == nullLog) { return; }

			// map events, timer, etc.
			try {
				log(string.Format("VSeismodensImpl.configure, SnapToLevels {0}", MapApplication.Current.Map.SnapToLevels));
				// init app staff
				frmElement = new VPseudoWindow() {
					app = this
				};
				rlForm = new RLAttribsForm();
				//polySymbol = rlForm.Resources["defaultAreaSymbol"] as SimpleFillSymbol;
				polySymbol = new ESRI.ArcGIS.Client.Symbols.SimpleFillSymbol() {
					Fill = new SolidColorBrush(Color.FromArgb(125, 255, 125, 0)),
					BorderBrush = new SolidColorBrush(Colors.Green),
					BorderThickness = 1.5
				};
				//FillSymbol = rlForm.Resources["drawAreaSymbol"] as SimpleFillSymbol
				drawSymbol = new ESRI.ArcGIS.Client.Symbols.SimpleFillSymbol() {
					Fill = new SolidColorBrush(Color.FromArgb(125, 255, 0, 125)),
					BorderBrush = new SolidColorBrush(Colors.Red),
					BorderThickness = 2.0
				};
				var toolbar = MapApplication.Current.FindObjectInLayout("MainToolbarContainer") as ContentControl;
				if(toolbar != null) {
					log(string.Format("VSeismodensImpl.configure, have toolbar"));
					int childCount = VisualTreeHelper.GetChildrenCount(toolbar);
					DependencyObject depObj;
					for(int i = 0; i < childCount; i++) {
						depObj = VisualTreeHelper.GetChild(toolbar, i);
						log(string.Format("VSeismodensImpl.configure, toolbar child {0}, obj {1}", i, depObj.GetType()));
						var depObj2 = depObj as ContentPresenter; //System.Windows.Controls.ContentPresenter
						int childCount2 = VisualTreeHelper.GetChildrenCount(depObj2);
						log(string.Format("VSeismodensImpl.configure, toolbar child childrens {0}, content '{1}'",
							childCount2, depObj2.Content));
						for(int j = 0; j < childCount2; j++) {
							var depObj3 = VisualTreeHelper.GetChild(depObj2, j);
							log(string.Format("VSeismodensImpl.configure, presenter child {0}, obj {1}", j, depObj3.GetType()));
						}
					}
					log(string.Format("VSeismodensImpl.configure done."));
				}
			}
			catch(Exception ex) {
				log(string.Format("VSeismodensImpl.configure, Initialization failed: {0}", ex.Message));
			}
		} // public void configure()


		/// <summary>
		/// call from ICommand.Execute
		/// </summary>
		public void runTool(object param) {
			log("VSeismodensImpl.runTool ...");
			// show rl layer
			var gl = getRLLayer();
			// init Draw
			if(draw == null) {
				log("runTool, create Draw");
				draw = new Draw(MapApplication.Current.Map) {
					FillSymbol = drawSymbol
				};
				draw.DrawComplete += evtDrawComplete;
				// Listen to the IsEnabled property.
				// This is to detect cases where other tools have
				// disabled the Draw surface. Close dialog window
				Utils.RegisterForNotification("IsEnabled", draw, frmElement, evtOnDrawEnabledChanged);
			}
			draw.IsEnabled = true;
			draw.DrawMode = DrawMode.Polygon;
		} // public void runTool(object param)


		/// <summary>
		/// Creates a simple graphics layer
		/// </summary>
		/// <returns>Redline layer</returns>
		private GraphicsLayer getRLLayer() {
			// call from runTool
			log("getRLLayer");
			var lyr = MapApplication.Current.SelectedLayer;
			var gl = VLayer.makeRLLayer(MapApplication.Current.Map, layerID, layerName);
			gl.MouseLeftButtonDown -= gl_MouseLeftButtonDown;
			gl.MouseLeftButtonDown += gl_MouseLeftButtonDown;
			MapApplication.Current.SelectedLayer = lyr;
			return gl;
		} // private GraphicsLayer getRLLayer()


		/// <summary>
		/// Geoprocessor service will be asked
		/// </summary>
		/// <param name="geom"></param>
		private void askGeoprocessor(ESRI.ArcGIS.Client.Geometry.Geometry geom) {
			var map = MapApplication.Current.Map;
			var poly = geom as ESRI.ArcGIS.Client.Geometry.Polygon;
			//poly = poly.Clone();
			//poly.SpatialReference = map.SpatialReference;
			log(string.Format("askGeoprocessor, spatialReference map wkid '{0}', map wkt '{1}', geom wkid '{2}', geom wkt '{3}'",
				map.SpatialReference.WKID, map.SpatialReference.WKT, poly.SpatialReference.WKID, poly.SpatialReference.WKT));
			double fSeismodens = 0, fProfilelength = 0, fShapeArea = 0; // result
			var oldCursor = MapApplication.Current.Map.Cursor;

			// todo: make gp as class member and check bisy state before asking.
			var gp = new Geoprocessor("http://cache.algis.com/ArcGIS/rest/services/" +
				"five/seismodens/GPServer/seismoprofiles%20density");
			gp.UpdateDelay = 300;
			var data = new List<GPParameter>();
			data.Add(new GPFeatureRecordSetLayer("inputPolygon", poly));

			gp.Failed += (sender, args) => {
				var tf = args as TaskFailedEventArgs;
				log(string.Format("gp.Failed, message {0}", tf.Error.Message));
				MapApplication.Current.Map.Cursor = oldCursor;
				MessageBox.Show(string.Format("Геопроцессор не может выполнить запрос \n {0}", tf.Error));
			}; // gp.Failed

			gp.JobCompleted += (sender, args) => {
				var ji = args as JobInfoEventArgs;
				string msgs = "";
				ji.JobInfo.Messages.ForEach(gpm => msgs += string.Format("\n{0}: {1}", gpm.MessageType, gpm.Description));
				log(string.Format("gp.JobCompleted, job status {0}, job id {1}, msgs {2}",
					ji.JobInfo.JobStatus, ji.JobInfo.JobId, msgs));
				MapApplication.Current.Map.Cursor = oldCursor;
				if(ji.JobInfo.JobStatus != esriJobStatus.esriJobSucceeded) {
					MessageBox.Show(string.Format("Геопроцессор не может выполнить запрос \n {0}", msgs));
					return;
				}

				gp.GetResultDataCompleted += (resSender, resArgs) => {
					var p = resArgs as GPParameterEventArgs;
					var dv = p.Parameter as GPDouble;
					var ci = new System.Globalization.CultureInfo("en-US");
					log(string.Format(ci, "gp.GetResultDataCompleted, param name '{0}', value '{1}'", p.Parameter.Name, dv.Value));
					if(p.Parameter.Name.Contains("seismoDens")) {
						fSeismodens = dv.Value;
						gp.GetResultDataAsync(ji.JobInfo.JobId, "profilesLength");
					}
					if(p.Parameter.Name.Contains("profilesLength")) {
						fProfilelength = dv.Value;
						gp.GetResultDataAsync(ji.JobInfo.JobId, "shapeArea");
					}
					if(p.Parameter.Name.Contains("shapeArea")) {
						fShapeArea = dv.Value;
						log(string.Format("askGeoprocessor, we got all the results, job done."));
						MessageBox.Show(string.Format(ci, "Сейсмоплотность {0} км/км2, \t\n суммарная длина профилей {1} км, \t\n " +
							"очерченная площадь {2} км2", fSeismodens, fProfilelength, fShapeArea));
					}
				}; // gp.GetResultDataCompleted

				gp.GetResultDataAsync(ji.JobInfo.JobId, "seismoDens");
			}; // gp.JobCompleted

			MapApplication.Current.Map.Cursor = System.Windows.Input.Cursors.Wait;
			gp.SubmitJobAsync(data); // http://help.arcgis.com/en/webapi/silverlight/help/index.html#/Geoprocessing_task/01660000000n000000/
		} // private void askGeoprocessor(ESRI.ArcGIS.Client.Geometry.Geometry geom)


		/// <summary>
		/// Plone service would asked
		/// </summary>
		/// <param name="geom"></param>
		private void sendRequest(ESRI.ArcGIS.Client.Geometry.Geometry geom) {
			var map = MapApplication.Current.Map;
			log(string.Format("sendRequest, map spatialReference wkid '{0}', map wkt '{1}'", map.SpatialReference.WKID, map.SpatialReference.WKT));
			// prepare url
			//http://site/rngis/mapsupport/seismicdensity?calcsrid=32&mapsrid=32&coords=1319562.926966302 5039833.556576901,1485013.350625462 5145120.189814549,1485013.350625462 4934546.923339254,1319562.926966302 5039833.556576901
			string shape = "";
			var poly = (ESRI.ArcGIS.Client.Geometry.Polygon)geom;
			var rings = poly.Rings;
			foreach(var pc in rings) {
				foreach(var mp in pc) {
					if(shape == "") { ; }
					else shape += ", ";
					shape += String.Format(new System.Globalization.CultureInfo("en-US"), "{0} {1}", mp.X, mp.Y);
				}
			}
			// config contains http://vdesk.algis.com:8088/rngis/mapsupport/seismicdensity?calcsrid=32&mapsrid=32
			string reqUrl = string.Format("{0}&coords={1}", baseUrl, shape);
			log(string.Format("sendRequest, url '{0}'", reqUrl));

			// send request
			WebClient wc = new WebClient();
			Uri u = new Uri(reqUrl, UriKind.RelativeOrAbsolute);

			wc.OpenReadCompleted += (sender, args) => {
				if(args.Error != null) {
					log(String.Format("wc_OpenReadCompleted, error in reading answer, msg [{0}], trace [{1}]",
						args.Error.Message, args.Error.StackTrace));
					MessageBox.Show("Сбой сервиса, подробности см.в отладочной консоли браузера");
					return;
				}
				try {
					StreamReader reader = new StreamReader(args.Result);
					string text = reader.ReadToEnd();
					log(string.Format("wc_OpenReadCompleted, resp '{0}'", text)); // [0.032,734.497,22623.010]
					// Фактически строка формата [сейсмоплотность км/км2,суммарная длина профилей км,очерченная площадь км2] где значения через запятую.
					var msg = this.parseResponse(text);
					MessageBox.Show(msg);
				}
				catch(Exception ex) {
					log(String.Format("wc_OpenReadCompleted, parsing failed {0}, {1}", ex.Message, ex.StackTrace));
					MessageBox.Show("Сервер вернул неформатный ответ, подробности см.в отладочной консоли браузера");
				}
			}; // wc.OpenReadCompleted

			wc.OpenReadAsync(u);
		} // private void sendRequest(ESRI.ArcGIS.Client.Geometry.Geometry geom)


		/// <summary>
		/// [сейсмоплотность км/км2,суммарная длина профилей км,очерченная площадь км2] где значения через запятую.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		private string parseResponse(string text) {
			var lst = text.Trim().Split(new string[] { "," }, StringSplitOptions.None);
			if(lst.Count() < 3) throw new Exception("response must contain 3 parts, e.g.'[0.032,734.497,22623.010]'");
			return string.Format("Сейсмоплотность {0} км/км2, \t\n суммарная длина профилей {1} км, \t\n " +
				"очерченная площадь {2} км2", lst[0].Replace("[", ""), lst[1], lst[2].Replace("]", ""));
		} // private string parseResponse(string text)


		void gl_MouseLeftButtonDown(object sender, GraphicMouseButtonEventArgs e) {
			// call from map LB event
			try {
				var gr = e.Graphic;
				if(gr == null) {
					log(string.Format("gl_MouseLeftButtonDown, e.Graphic is null"));
					return;
				}
				e.Handled = true;
				log("gl_MouseLeftButtonDown.editFeature(gr)");
				var gl = getRLLayer();
				gl.ClearGraphics();
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой редактирования пометки: \n [{0}]", ex.Message);
				//MessageBox.Show(msg);
				log(msg);
			}
		} // void gl_MouseLeftButtonDown(object sender, GraphicMouseButtonEventArgs e)


		private void evtDrawComplete(object sender, DrawEventArgs e) {
			// call from Map when user stop draw current mark
			//from exsample
			//ESRI.ArcGIS.Client.Graphic graphic = new ESRI.ArcGIS.Client.Graphic()
			//{ Geometry = args.Geometry, Symbol = _activeSymbol };
			//graphicsLayer.Graphics.Add(graphic);
			log("evtDrawComplete");

			currGeom = e.Geometry;
			frmElement.disabledDrawInternally = true;
			draw.DrawMode = DrawMode.None;
			draw.IsEnabled = false;

			try {
				var gl = getRLLayer();
				var graphic = new ESRI.ArcGIS.Client.Graphic() {
					Geometry = currGeom, Symbol = polySymbol
				};
				gl.Graphics.Add(graphic);
				log("evtDrawComplete, graphic added");

				// sendRequest(currGeom); // Plone service

				//askGeoprocessor(currGeom); // Geoprocessor service
				// http://help.arcgis.com/en/webapi/silverlight/samples/start.htm#Simplify
				// http://tasks.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer/simplify?sr=102100&geometries=%7B%0D%0A%09%22geometryType%22%3A%22esriGeometryPolygon%22%2C%0D%0A%09%22geometries%22%3A%5B%7B%0D%0A%09%09%22rings%22%3A%5B%5B%0D%0A%09%09%09%5B7827152.02924901%2C9666532.33347098%5D%2C%0D%0A%09%09%09%5B8316349.01027401%2C10057889.918291%5D%2C%0D%0A%09%09%09%5B7729312.63304402%2C10214432.952219%5D%2C%0D%0A%09%09%09%5B7827152.02924901%2C9666532.33347098%5D%5D%5D%0D%0A%09%7D%5D%0D%0A%7D&f=HTML
				var oldCursor = MapApplication.Current.Map.Cursor;
				var geometryService = new GeometryService("http://tasks.arcgisonline.com/ArcGIS/rest/services/" +
					"Geometry/GeometryServer");
				var graphicList = new List<Graphic>();
				graphicList.Add(graphic);

				geometryService.SimplifyCompleted += (sndr, args) => {
					MapApplication.Current.Map.Cursor = oldCursor;
					log("evtDrawComplete, SimplifyCompleted OK");
					gl.Graphics.Remove(graphic);
					graphic.Geometry = args.Results[0].Geometry;
					gl.Graphics.Add(graphic);
					log("evtDrawComplete, askGeoprocessor...");
					askGeoprocessor(graphic.Geometry);
				}; // geometryService.SimplifyCompleted

				geometryService.Failed += (sndr, args) => {
					MapApplication.Current.Map.Cursor = oldCursor;
					log(string.Format("evtDrawComplete, SimplifyCompleted err {0}", args.Error));
					MessageBox.Show("Сбой нормализации полигона: " + args.Error);
				}; // geometryService.Failed

				MapApplication.Current.Map.Cursor = System.Windows.Input.Cursors.Wait;
				log("evtDrawComplete, asking SimplifyAsync");
				geometryService.SimplifyAsync(graphicList);
				log("evtDrawComplete, wait for GeometryServer...");
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой отправки запроса: \n [{0}]", ex.Message);
				log(msg);
				MessageBox.Show(msg);
			}
		} // private void evtDrawComplete(object sender, DrawEventArgs e)


		/// <summary>
		/// Fires when the Draw surface is enabled or disabled
		/// for hide dialog window if Draw disabled
		/// or for show tooltip like 'RL disabled, another tool is working'
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
		private static void evtOnDrawEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var wnd = d as VPseudoWindow;
			if(wnd.app != null) wnd.app.log("evtOnDrawEnabledChanged");

			if(e.OldValue != null && !(bool)e.NewValue &&
				!wnd.disabledDrawInternally) {
				//MapApplication.Current.HideWindow(wnd);
				wnd.Visibility = Visibility.Collapsed;
				//wnd.app.rlToolbar.Visibility = Visibility.Collapsed;
			}
			else if(wnd.disabledDrawInternally)
				wnd.disabledDrawInternally = false;
		} // evtOnDrawEnabledChanged

	} // public class VSeismodensImpl: Object 

} // namespace mwb02.AddIns
