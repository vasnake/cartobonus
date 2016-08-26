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
using System.ComponentModel.Composition;
//using System.ComponentModel;

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Linq;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;

using System.Json;
using System.Security.Cryptography;
using System.Windows.Browser;

namespace mwb02.AddIns {

	[Export(typeof(ICommand))]
	[DisplayName("Save")]
	[Description("Сохранить карту")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/save.png")]
	public class VSave: ICommand, ISupportsConfiguration {
		private MyConfigDialog configDialog = new MyConfigDialog();
		private SaveWnd saveWnd = new SaveWnd();
		private debug dbgDialog = new debug();
		private string currentCfg = "";

		public VSave() {
			dbgDialog.textBlock1.Text = "Save log:\n";
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
		public void Execute(object parameter) {
			//MapApplication.Current.ShowWindow("Debug messages", dbgDialog, false);
			log("VSave.Execute, ...");

			if(saveWnd.textBox1.Text == "") {
				DateTime dt = DateTime.Now;
				saveWnd.textBox1.Text = string.Format("{0}", dt.ToString("yyyy-MM-dd"));
			}
			saveWnd.vmsApp = this;
			MapApplication.Current.ShowWindow("Имя для карты", saveWnd, false);
			saveMap2Server();
			log("VSave.Execute, wait for user...");
		} // public void Execute(object parameter)


		public bool CanExecute(object parameter) {
			return MapApplication.Current.Map != null;
		} // public bool CanExecute(object parameter)

		public event EventHandler CanExecuteChanged;

		#endregion // #region ICommand members


		public void log(String txt) {
			DateTime dt = DateTime.Now;
			var d = this.dbgDialog.textBlock1;
			var msg = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			d.Text += msg;
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)


		/// <summary>
		/// Save map config to server.
		/// Send http req. and set location.hash in brawser
		/// </summary>
		private void saveMap2Server() {
			currentCfg = getMapConfig();
			var hash = VExtClass.computeSHA1Hash(currentCfg).ToLower();
			log(string.Format("VSave.save2Server, hash='{0}'", hash)); // hash='310342AE3F6C2E7FD2C4C9FCC146E5ABB1E5A670'

			// c:\Inetpub\wwwroot\Apps\app3\Config\Tools.xml
			// <Tool.ConfigData>http://vdesk.algis.com/kvsproxy/Proxy.ashx</Tool.ConfigData>
			var servUrl = configDialog.InputTextBox.Text;
			log(string.Format("VSave.save2Server, base url='{0}'", servUrl));

			try {
				HtmlWindow window = HtmlPage.Window;
				var func = (window.Eval("saveMap2Server") as ScriptObject);
				func.InvokeSelf(servUrl, hash, currentCfg);

				sendSaveRequest(servUrl, hash, currentCfg);
			}
			catch(Exception ex) {
				var msg = string.Format("VSave.save2Server failed, error: \n {0}", ex.Message);
				log(msg);
				//MessageBox.Show(msg);
			}
		} // private void saveMap2Server()


		private void sendSaveRequest(string servUrl, string key, string data) {
			WebClient wc = new WebClient();
			wc.UploadStringCompleted += new UploadStringCompletedEventHandler(wc_UploadStringCompleted);
			wc.UploadProgressChanged += new UploadProgressChangedEventHandler(wc_UploadProgressChanged);
			try {
				// http://vdesk.algis.com/kvsproxy/Proxy.ashx?id=foo&text=bar
				var url = string.Format("{0}", servUrl);
				Uri u = new Uri(url, UriKind.RelativeOrAbsolute);
				//data = System.Windows.Browser.HttpUtility.UrlEncode(data);
				string strData = String.Format("id={0}&amp;text={1}", key, data);
				wc.UploadStringAsync(u, "POST", strData);
				log(String.Format("VSave.sendSaveRequest, request sent, url [{0}]...", url));
			}
			catch(Exception ex) {
				log(String.Format("VSave.sendSaveRequest failed, error '{0}'", ex.Message));
			}
		} // private void sendSaveRequest(string servUrl, string key, string data)

		void wc_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e) {
			log(String.Format(
				"VSave.UploadProgress % [{0}]", e.ProgressPercentage));
		} // void wc_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)

		void wc_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e) {
			log("VSave.wc_UploadStringCompleted, ...");
			if(e.Error != null) {
				log(String.Format("VSave.wc_UploadStringCompleted, request failed: msg [{0}], trace [{1}]", e.Error.Message, e.Error.StackTrace));
				return;
			}
			try {
				string res = e.Result;
				log(String.Format("VSave.wc_UploadStringCompleted, result [{0}]", res));
			}
			catch(Exception ex) {
				log(String.Format("VSave.wc_UploadStringCompleted, ошибка разбора ответа [{0}]", ex.Message));
			}
		} // void wc_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e)


		internal void saveMap(string mapname) {
			log(string.Format("VSave.saveMap, mapname [{0}] ...", mapname));
			if(currentCfg == "") currentCfg = getMapConfig();

			VFileStorage stor = openStorage(mapname);
			stor.write(currentCfg);
			stor.close();
			// if all ok
			MapApplication.Current.HideWindow(saveWnd);
			log(string.Format("VSave.saveMap, done."));
		} // internal void saveMap(string mapname)


		private string getMapConfig() { // read layers list, create json object from them
			log("VSave.getMapConfig, ...");

			ESRI.ArcGIS.Client.Map map = MapApplication.Current.Map;
			var cfg = VSave.mapConfig(map);

			log(string.Format("VSave.getMapConfig, done. res=[{0}]", cfg));
			return cfg;
		} // private string getMapConfig()


		/// <summary>
		/// read layers list, create json object from them
		/// </summary>
		/// <param name="map"></param>
		/// <returns></returns>
		public static string mapConfig(ESRI.ArcGIS.Client.Map map) {
			var res = new JsonObject(); // http://msdn.microsoft.com/en-us/library/cc197957%28v=VS.95%29.aspx

			ESRI.ArcGIS.Client.LayerCollection lyrs = map.Layers;
			var ext = map.Extent.Clone();

			var obj = new JsonObject {
				{"xmin", ext.XMin},
				{"xmax", ext.XMax},
				{"ymin", ext.YMin},
				{"ymax", ext.YMax},
				{"sridWKID", ext.SpatialReference.WKID},
				{"sridWKT", ext.SpatialReference.WKT}
			};
			res.Add("MapExtent", obj);

			int num = 0;
			foreach(var lyr in lyrs) {
				// from bottom to top
				var vl = new VLayer(lyr);
				if(vl.ID == null || vl.ID == "") {
					string.Format("VSave.mapConfig, skip layer '{0}'", vl.toJson()).clog();
					continue; // skip empty GraphicsLayer
				}
				num += 1;
				obj = vl.toJson();
				res.Add(num.ToString(), obj);
			} // end foreach layer

			return res.ToString();
		} // public static string mapConfig(ESRI.ArcGIS.Client.Map map)


		private VFileStorage openStorage(string filename) { // create and init storage object
			log("VSave.openStorage, ...");
			var res = new VFileStorage(log);
			res.create(filename);
			log("VSave.openStorage, done.");
			return res;
		}

	} // public class VSave : ICommand, ISupportsConfiguration   


	public class VRestoreFromServ: Object {

		public logFunc log;
		public void nullLog(string msg) { msg.clog(); }
		public string serverURL = "";
		public string cfgHash = "";
		public VRestore app = null;

		public VRestoreFromServ(logFunc l) {
			log = l;
			configure(null, null);
		}
		public VRestoreFromServ() {
			log = nullLog;
			configure(null, null);
		}
		public void configure(string servUrl, VRestore plug) {
			log(string.Format("VRestoreFromServ.configure..."));
			if(log == nullLog) { return; }

			try {
				log(string.Format("VRestoreFromServ.configure, SnapToLevels {0}", MapApplication.Current.Map.SnapToLevels));
				if(servUrl == null) { ; }
				else {
					this.app = plug;
					serverURL = servUrl;
					log(string.Format("VRestoreFromServ.configure, stor.server url='{0}'", serverURL));
					doRestoreMap(serverURL);
				}
				log(string.Format("VRestoreFromServ.configure done."));
			}
			catch(Exception ex) {
				log(string.Format("VRestoreFromServ.configure, Initialization failed: {0}", ex.Message));
			}
		} // public void configure()


		public void doRestoreMap(string baseurl) {
			log(string.Format("VRestoreFromServ.doRestore..."));
			// http://vdesk.algis.com/Apps/app3/index.htm#940213BE6D82CEC72CDA9CBC6976E534A1C232B4
			HtmlWindow window = HtmlPage.Window;
			var func = (window.Eval("getLocationHash") as ScriptObject);
			var res = func.InvokeSelf() as string;
			log(string.Format("VRestoreFromServ.doRestore, hash='{0}'", res));
			if(res == "") {
				log(string.Format("VRestoreFromServ.doRestore done, hash is null"));
				return;
			}
			else {
				cfgHash = res;
				log(string.Format("VRestoreFromServ.doRestore, proceed cfg retrieval..."));
				readConfig(baseurl, res);
			}
		} // public void doRestoreMap()


		public void readConfig(string baseurl, string key) {
			WebClient wc = new WebClient();
			wc.OpenReadCompleted += new OpenReadCompletedEventHandler(wc_OpenReadCompleted);
			// http://vdesk.algis.com/kvsproxy/Proxy.ashx?get=foo
			string url = string.Format("{0}?get={1}", baseurl, key);
			Uri u = new Uri(url, UriKind.RelativeOrAbsolute);
			wc.OpenReadAsync(u);
			log(String.Format("VRestoreFromServ.readConfig, request sent, url [{0}]...", url));
		}


		void wc_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
			log("VRestoreFromServ.wc_OpenReadCompleted, get service response, parse...");
			if(e.Error != null) {
				log(String.Format("VRestoreFromServ.wc_OpenReadCompleted, error in reading answer, msg [{0}], trace [{1}]",
					e.Error.Message, e.Error.StackTrace));
				return;
			}
			try {
				StreamReader reader = new StreamReader(e.Result);
				string text = reader.ReadToEnd();
				var hash = VExtClass.computeSHA1Hash(text).ToLower();
				log(String.Format("VRestoreFromServ.wc_OpenReadCompleted, hash='{1}', config=[{0}]", text, hash));
				if(hash.CompareTo(cfgHash) == 0) {
					log(String.Format("VRestoreFromServ.wc_OpenReadCompleted, hash OK, load config..."));
					this.app.applyConfig(text);
				}
				else {
					log(String.Format("VRestoreFromServ.wc_OpenReadCompleted, hash not OK, abort loading."));
				}
			}
			catch(Exception ex) {
				log(String.Format("VRestoreFromServ.wc_OpenReadCompleted, загрузки сейва [{0}]", ex.Message));
			}
		}
	} // public class VRestoreFromServ: Object


	[Export(typeof(ICommand))]
	[DisplayName("Restore")]
	[ESRI.ArcGIS.Client.Extensibility.Category("CGIS Tools")]
	[ESRI.ArcGIS.Client.Extensibility.Description("Восстановить сохраненную карту")]
	[DefaultIcon("/mwb02.AddIns;component/Images/restore.png")]
	public class VRestore: ICommand, ISupportsConfiguration {
		private MyConfigDialog configDialog = new MyConfigDialog();
		private RestoreWnd restoreWnd = new RestoreWnd();
		private debug dbgDialog = new debug();

		private List<VLayer> vLayers = null;
		private string mapCfg;
		public VConcurrentQueue lyrsQueue = new VConcurrentQueue();
		private VLayer selectedLyr = null;
		private int mapProgress = 0;
		//private bool lyrsInited = false;
		private string msg = "";
		private VRestoreFromServ vSSave = new VRestoreFromServ();

		public VRestore() {
			dbgDialog.textBlock1.Text = "Restore log:\n";
			vSSave = new VRestoreFromServ(this.log);
		}

		#region ISupportsConfiguration members

		public void Configure() {
			// When the dialog opens, it shows the information saved from the last configuration
			MapApplication.Current.ShowWindow("Configuration", configDialog);
		} // public void Configure()


		public void LoadConfiguration(string configData) {
			// Initialize the behavior's configuration with the saved configuration data. 
			// The dialog's textbox is used to store the configuration.
			configDialog.InputTextBox.Text = configData; // http://vdesk.algis.com/kvsproxy/Proxy.ashx
			log(string.Format("VRestore.LoadConfiguration, config='{0}'", configData));
			vSSave.configure(configDialog.InputTextBox.Text, this);
		} // public void LoadConfiguration(string configData)


		public string SaveConfiguration() {
			// Save the information from the configuration dialog
			return configDialog.InputTextBox.Text;
		} // public string SaveConfiguration()
		#endregion // #region ISupportsConfiguration members


		#region ICommand members
		public void Execute(object parameter) {
			//MapApplication.Current.ShowWindow("Debug messages", dbgDialog, false);
			log("VRestore.Execute, ...");

			restoreWnd.vmsApp = this;
			restoreWnd.listBox1.Items.Clear();
			var saves = getSavesList();
			foreach(string fn in saves) {
				restoreWnd.listBox1.Items.Add(fn);
			}
			MapApplication.Current.ShowWindow("Имя карты", restoreWnd, false);
			log("VRestore.Execute, wait for user...");
		} // public void Execute(object parameter)

		public bool CanExecute(object parameter) {
			return MapApplication.Current.Map != null;
		} // public bool CanExecute(object parameter)

		public event EventHandler CanExecuteChanged;

		#endregion // #region ICommand members


		private List<string> getSavesList() // var saves = getSevesList();
		{
			var store = new VFileStorage(log);
			var res = store.getFilesList(); // new List<string>();
			return res;
		}

		internal void deleteMap(string filename) {
			var stor = new VFileStorage(log);
			stor.open(filename);
			string cfg = stor.read();
			log(string.Format("VRestore.deleteMap, fname [{0}], content [{1}]", filename, cfg));
			stor.delete(filename);
			return;

			//restoreWnd.listBox1.Items.Clear();
			//var saves = getSavesList();
			//foreach(string fn in saves) {
			//restoreWnd.listBox1.Items.Add(fn);
			//}
		} // internal void deleteMap(string filename) 


		internal void restoreMap(string filename) { // invoke from OK button in 'restore from save' window: this.vmsApp.restoreMap(filename);
			log(string.Format("VRestore.restoreMap, mapname [{0}] ...", filename));
			string cfg = readMapConfig(filename); // from VFileStorage
			applyConfig(cfg); // remove layers, create layers
			// if all ok
			//MapApplication.Current.HideWindow(restoreWnd);
			log(string.Format("VRestore.restoreMap, done."));
			//throw new Exception("VRestore.restoreMap, not yet");
		} // internal void restoreMap(string filename)


		private string readMapConfig(string filename) { //invoke from restoreMap method: string cfg = readMapConfig(filename); // from VFileStorage
			// return json string from save
			var stor = new VFileStorage(log);
			stor.open(filename);
			string cfg = stor.read();
			log(string.Format("VRestore.readMapConfig, cfg [{0}]", cfg));
			return cfg;
			//throw new Exception("VRestore.readMapConfig, not yet");
		}


		/// <summary>
		/// Invoke from restoreMap method: applyConfig(cfg);
		/// get layers list, start ping services pingLyrsList
		/// </summary>
		/// <param name="cfg"></param>
		public void applyConfig(string cfg) {
			log(string.Format("VRestore.applyConfig"));
			// check if vLayers is not null already!
			if(vLayers != null) {
				throw new Exception("Подождите завершения загрузки предыдущей карты");
			}
			MapApplication.Current.Map.Cursor = System.Windows.Input.Cursors.Wait;
			// restore map layers from json string
			var layersList = lyrsListFromConfig(cfg);
			this.mapCfg = cfg;
			this.vLayers = layersList;

			this.selectedLyr = null;
			foreach(var vl in layersList) {
				if(vl.selected) this.selectedLyr = vl;
			}
			log(string.Format("VRestore.applyConfig, selectedLyr '{0}' '{1}'", selectedLyr.ID, selectedLyr.lyrName));

			pingLyrsList(layersList);

			// only after ping all layers
			//loadLyrsList(layersList);
		} // private void applyConfig(string cfg)


		/// <summary>
		/// Call from applyConfig. Parse saved map config
		/// </summary>
		/// <param name="cfg"></param>
		/// <returns></returns>
		public static List<VLayer> lyrsListFromConfig(string cfg) {
			var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cfg));
			var js = JsonObject.Load(ms);

			var layersList = new List<VLayer>();
			for(int cnt = 1; cnt < 1000; cnt++) {
				if(js.ContainsKey(cnt.ToString())) {
					var jsLyr = js[cnt.ToString()];
					var vLyr = new VLayer((JsonObject)jsLyr);
					//add to LayersList
					if(vLyr.lyr.GetType() == typeof(ESRI.ArcGIS.Client.GraphicsLayer)) {
						layersList.Add(vLyr);
					}
					else {
						layersList.Add(vLyr);
					}
				}
				else {
					break;
				}
			} // fill layersList from json

			return layersList;
		} // private List<VLayer> lyrsListFromConfig(string cfg) 


		public static ESRI.ArcGIS.Client.Geometry.Envelope mapExtentFromConfig(string cfg) {
			ESRI.ArcGIS.Client.Geometry.Envelope res = null;
			var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cfg));
			var js = JsonObject.Load(ms);
			// "MapExtent":{"xmin":14863613.810013881,"xmax":7260708.5358984508,"ymin":6355694.1210022084,"ymax":7260708.5358984508,"sridWKID":102100}
			if(js.ContainsKey("MapExtent")) {
				var jsExt = js["MapExtent"];
				res = new ESRI.ArcGIS.Client.Geometry.Envelope(
					x1 : jsExt["xmin"],
					y1 : jsExt["ymin"],
					x2 : jsExt["xmax"],
					y2 : jsExt["ymax"]
				);
				res.SpatialReference = new ESRI.ArcGIS.Client.Geometry.SpatialReference(WKID : jsExt["sridWKID"]);
				//res.SpatialReference = new ESRI.ArcGIS.Client.Geometry.SpatialReference(wkt : jsExt["sridWKT"]);
			}
			return res;
		} // public static ESRI.ArcGIS.Client.Geometry.Envelope mapExtentFromConfig(string cfg)


		private void pingLyrsList(List<VLayer> layersList) {
			// call from applyConfig
			// слои ставятся в очередь, для каждого слоя из очереди выполняется «пинг».
			// При ответе на пинг слой убирается из очереди. Когда очередь опустеет,
			// слои пакетом добавляются в карту
			log(string.Format("VRestore.pingLyrsList, lyrs {0}", layersList.Count));
			foreach(var x in layersList) {
				if(x.lyr.GetType() == typeof(ESRI.ArcGIS.Client.GraphicsLayer)) {
					continue;
				}
				this.lyrsQueue.setLock();
				this.lyrsQueue.Enqueue(x);
				lyrsQueue.hasProcessor = true;
				this.lyrsQueue.releaseLock();
				pingService(x);
			}
		} // private void pingLyrsList(List<VLayer> layersList)


		/// <summary>
		/// Ping layer service; in callback pick next layer from queue; if next is null call loadMapCfg
		/// </summary>
		/// <param name="lyr"></param>
		public void pingService(VLayer lyr) {
			restoreWnd.busyIndicator1.IsBusy = true;
			var wc = new WebClient();
			wc.OpenReadCompleted += pingsrv_OpenReadCompleted;
			string url = lyr.lyrUrl + "?f=json&pretty=true";
			try {
				Uri u = new Uri(url, UriKind.RelativeOrAbsolute);
				//this.listDialog.busyIndicator1.IsBusy = true;
				wc.OpenReadAsync(u, url);
				log(String.Format("pingService, request sent, url [{0}]...", url));
			}
			catch(Exception e) {
				log(String.Format("pingService, catch exception while open url [{0}], msg [{1}]", url, e.Message));
				pingsrv_OpenReadCompleted(null, null);
			}
		} // public void pingService(VLayer lyr) 


		/// <summary>
		/// Layer service ping callback. Peek next layer; if next is null call loadMapCfg.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void pingsrv_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
			//listDialog.busyIndicator1.IsBusy = false;
			try {
				var wc = sender as WebClient;
				var url = e.UserState as string;
				log(String.Format("VRestore.pingsrv_OpenReadCompleted, got responce, url '{0}'", url));

				//Stream stream = (Stream)e.Result;
				//BinaryReader reader = new BinaryReader(stream);
				//byte[] buffer = reader.ReadBytes((int)stream.Length);
				//var str = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)stream.Length);

				if(e != null && e.Error != null) {
					log(String.Format("VRestore.pingsrv_OpenReadCompleted, resp.Error: "
						+ "url '{1}', msg [{0}]", e.Error.Message, url));
				}

				lyrsQueue.setLock();
				var lyr = lyrsQueue.Dequeue() as VLayer;
				var next = lyrsQueue.Peek() as VLayer;
				if(next == null) lyrsQueue.hasProcessor = false;
				lyrsQueue.releaseLock();

				//e.Result
				if(next == null) {
					log(String.Format("VRestore.pingsrv_OpenReadCompleted, all services was pinged."));
					restoreWnd.busyIndicator1.IsBusy = false;
					this.selectedLyr = loadMapCfg(mapCfg, MapApplication.Current.Map, lyr_InitializationFailed, Map_Progress);
					vLayers = null;
					MapApplication.Current.HideWindow(restoreWnd);
				}
			}
			catch(Exception ex) {
				log(String.Format("VRestore.pingsrv_OpenReadCompleted, exception '{0}'", ex.Message));
			}
		} // void pingsrv_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {


		/// <summary>
		/// If layers in config: clear map.layers; attach mapprogress event; add layers from config to map;
		/// set extent; set selected layer
		/// </summary>
		/// <param name="cfg"></param>
		/// <param name="map"></param>
		/// <param name="lyrInitFail"></param>
		/// <param name="mapProgress"></param>
		public static VLayer loadMapCfg(string cfg, ESRI.ArcGIS.Client.Map map,
			EventHandler<EventArgs> lyrInitFail, EventHandler<ProgressEventArgs> mapProgress) {
			// if LayersList: clean map; add layers from LayersList to map
			ESRI.ArcGIS.Client.Geometry.Envelope ext = VRestore.mapExtentFromConfig(cfg);
			var layersList = VRestore.lyrsListFromConfig(cfg);
			if(layersList.Count <= 0) {
				throw new Exception("VRestore.loadMapCfg: список слоев пуст, видимо была сохранена пустая карта");
			}

			ESRI.ArcGIS.Client.LayerCollection lyrs = map.Layers;
			// clear map
			lyrs.Clear();
			VLayer sl = null;
			if(mapProgress != null && layersList.Count > 0) {
				map.Progress -= mapProgress;
				map.Progress += mapProgress;
			}

			// add layers to map
			foreach(var x in layersList) {
				//string.Format("loadMapCfg, add layer {0}", x.lyrUrl).clog();
				string.Format("VRestore.loadMapCfg, add layer '{0}' '{1}' '{2}'", x.ID, x.lyrName, x.lyrType).clog();
				if(lyrInitFail != null) x.lyr.InitializationFailed += new EventHandler<EventArgs>(lyrInitFail);
				//x.lyr.SetValue(MapApplication.LayerNameProperty, x.lyrName);
				//x.lyr.Initialize();
				lyrs.Add(x.lyr);
				MapApplication.SetLayerName(x.lyr, x.lyrName);
				if(x.selected) sl = x;
			}
			if(ext != null) map.Extent = ext;

			// select selected layer
			if(sl != null) {
				string.Format("VRestore.loadMapCfg, selected layer '{0}' '{1}'", sl.ID, sl.lyrName).clog();
				MapApplication.Current.SelectedLayer = sl.lyr;
			}
			return sl;
		} // public static VLayer loadMapCfg(string cfg, ESRI.ArcGIS.Client.Map map)


		void lyr_InitializationFailed(object sender, EventArgs e) {
			Layer layer = sender as Layer;
			var lyr = new VLayer(layer);
			string mesg = "";
			if(layer.InitializationFailure != null) {
				mesg = layer.InitializationFailure.ToString() + "\n";
			}
			mesg = string.Format(
				"Невозможно включить слой [{0}], \n ошибка [{1}]", lyr.lyrUrl, mesg);
			log(string.Format("lyr_InitializationFailed, [{0}]", mesg));
			//MessageBox.Show(msg);
			this.msg += mesg;
			layer.Visible = false;
		} // void lyr_InitializationFailed(object sender, EventArgs e) 


		/// <summary>
		/// set SelectedLayer property when map 100% loaded.
		/// Don't work if map has FeatureLayer that take Selected property on himself
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Map_Progress(object sender, ProgressEventArgs e) {
			mapProgress = e.Progress;
			if(mapProgress < 100) return;
			log(string.Format("VRestore.Map_Progress [{0}] ", e.Progress));

			foreach(var x in MapApplication.Current.Map.Layers) {
				if(x.IsInitialized == false) {
					mapProgress = 0;
					log("VRestore.Map_Progress, jump from 100 to 0");
					break;
				}
			}
			if(mapProgress < 100) return;

			//lyrsInited = true;
			if(selectedLyr != null) {
				foreach(var x in MapApplication.Current.Map.Layers) {
					if(x.ID != null && (x.ID == selectedLyr.ID)) {
						MapApplication.Current.SelectedLayer = x;

						if(x.Equals(selectedLyr.lyr)) {
							log(string.Format("VRestore.Map_Progress, stored selectedLyr is actual layer"));
						} else {
							log(string.Format("VRestore.Map_Progress, stored selectedLyr is not actual layer"));
						}
						break;
					}
				}
				//MapApplication.Current.SelectedLayer = selectedLyr.lyr;
				log(string.Format("VRestore.Map_Progress, set selectedLayer to '{0}' '{1}'", selectedLyr.ID, selectedLyr.lyrName));
			}
			else {
				log(string.Format("VRestore.Map_Progress, selectedLayer == null"));
			}

			MapApplication.Current.Map.Progress -= Map_Progress;

			// deferred call workaround
			WebClient wc = new WebClient();
			wc.OpenReadCompleted += (wcSender, wcEargs) => {
				MapApplication.Current.Map.Cursor = System.Windows.Input.Cursors.Arrow;
				var sl = MapApplication.Current.SelectedLayer;
				var layerName = sl.GetValue(MapApplication.LayerNameProperty) as string;
				log("VRestore.Map_Progress, deferred, selected layer: '" + layerName + "'");
				MapApplication.Current.SelectedLayer = selectedLyr.lyr;
				log(string.Format("VRestore.Map_Progress, deferred, final set SelectedLayer to '{0}' '{1}'", selectedLyr.ID, selectedLyr.lyrName));
			};
			wc.OpenReadAsync(new Uri("http://www.google.com/search?q=access+the+variable+directly+using+a+closure+proxy.OpenReadCompleted", UriKind.Absolute));
			// http://ajax.googleapis.com/ajax/services/search/web?v=1.0&q=access+the+variable+directly+using+a+closure+proxy.OpenReadCompleted

			if(msg != "") MessageBox.Show(msg); msg = "";
		} // void Map_Progress(object sender, ProgressEventArgs e)


		public void log(String txt) {
			DateTime dt = DateTime.Now;
			var d = this.dbgDialog.textBlock1;
			//var d = this.dbgResizable.textBlock1;
			var msg = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			d.Text += msg;
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		}

	} // public class VRestore : ICommand, ISupportsConfiguration

}
