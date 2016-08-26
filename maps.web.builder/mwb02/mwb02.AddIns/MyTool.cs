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

using System.IO.IsolatedStorage;
using System.Json;
using System.Linq;
using System.Xml.Linq;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;

namespace mwb02.AddIns {

	[Export(typeof(ICommand))]
	[DisplayName("Data Collection")]
	[Description("Доступ к коллекции картслужб")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/AddContent32.png")]
	public class VMSCollection: ICommand, ISupportsConfiguration {

		private MyConfigDialog configDialog = new MyConfigDialog();
		private layerslist listDialog = new layerslist();
		private debug dbgDialog = new debug();
		public List<VLayerDescription> lyrsDescr = new List<VLayerDescription>();
		public VConcurrentQueue lyrsQueue = new VConcurrentQueue();

		public void log(String txt) {
			DateTime dt = DateTime.Now;
			var d = this.dbgDialog.textBlock1;
			var msg = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			d.Text += msg;
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

		#region ISupportsConfiguration members

		public void Configure() {
			// When the dialog opens, it shows the information saved from the last configuration
			MapApplication.Current.ShowWindow("Configuration", configDialog);
		}

		public void LoadConfiguration(string configData) {
			// Initialize the behavior's configuration with the saved configuration data. 
			// The dialog's textbox is used to store the configuration.
			configDialog.InputTextBox.Text = configData;
		}

		public string SaveConfiguration() {
			// Save the information from the configuration dialog
			return configDialog.InputTextBox.Text;
		}

		#endregion

		#region ICommand members

		public void Execute(object parameter) {
			//MapApplication.Current.ShowWindow("Debug messages", dbgDialog, false);
			dbgDialog.textBlock1.Text = "";

			log("Execute, open layers list...");
			listDialog.InputTextBox.Text = configDialog.InputTextBox.Text;
			listDialog.vmsApp = this;
			MapApplication.Current.ShowWindow("Доступные слои", listDialog, false);
			log("Execute, listWindow opened, ask service for XML, wait please...");

			WebClient wc = new WebClient();
			wc.OpenReadCompleted += new OpenReadCompletedEventHandler(wc_OpenReadCompleted);
			string url = configDialog.InputTextBox.Text;
			try {
				Uri u = new Uri(url, UriKind.RelativeOrAbsolute);
				wc.OpenReadAsync(u);
				log(String.Format("Execute, request sent, url [{0}]...", url));
			}
			catch(Exception e) {
				log(String.Format("Execute, catch exception while open url [{0}], msg [{1}]", url, e.Message));
			}
		}

		public bool CanExecute(object parameter) {
			return MapApplication.Current.Map != null;
		}

		public event EventHandler CanExecuteChanged;

		#endregion


		void wc_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
			log("wc_OpenReadCompleted, get service response, parse...");
			if(e.Error != null) {
				log(String.Format("wc_OpenReadCompleted, error in reading answer, msg [{0}], trace [{1}]", e.Error.Message, e.Error.StackTrace));
				return;
			}
			try {
				fillLyrList(e.Result);
			}
			catch(Exception ex) {
				log(String.Format("wc_OpenReadCompleted, Ошибка разбора списка [{0}]", ex.Message));
			}
		} // void wc_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {

		private void fillLyrList(System.IO.Stream s) {
			log("fillLyrList...");
			List<VLayerDescription> lyrs = new List<VLayerDescription>();
			XDocument doc = XDocument.Load(s);
			var res = from r in doc.Descendants("Layer")
					  where r.Attribute("id").Value != ""
					  select r;
			foreach(var r in res) {
				VLayerDescription ld = new VLayerDescription();
				ld.id = r.Attribute("id").Value.Trim();
				ld.name = r.Attribute("name").Value.Trim();
				ld.type = r.Attribute("type").Value.Trim();
				ld.topic = r.Attribute("topic").Value.Trim();
				ld.url = r.Value.ToString().Trim();
				ld.proxy = ld.getFromXml(r.Attribute("proxy"));
				ld.preview = ld.getFromXml(r.Attribute("preview"));
				ld.imageFormat = ld.getFromXml(r.Attribute("ImageFormat"));
				log(String.Format("get LD [{0}]", ld.toString()));
				lyrs.Add(ld);
			}
			log("fillLyrList, got layers: " + String.Join("; ", lyrs));
			this.lyrsDescr = lyrs;

			this.fillListDialog();
		} // private void fillLyrList(System.IO.Stream s)


		public void fillListDialog() {
			log("fillListDialog, draw list...");
			this.listDialog.listBox1.Items.Clear();
			//this.listDialog.listBox2.Items.Clear();

			this.listDialog.listBox2.ItemsSource = this.lyrsDescr;
			foreach(VLayerDescription ld in this.lyrsDescr) {
				//this.listDialog.listBox2.Items.Add(ld);
				if(!this.listDialog.listBox1.Items.Contains(ld.topic)) {
					this.listDialog.listBox1.Items.Add(ld.topic);
				}
			}
		} // public void fillListDialog() {


		public void changeTopicSelection() {
			log("changeTopicSelection...");
			string topic = this.listDialog.listBox1.SelectedItem.ToString();
			log("selected topic: " + topic);

			//this.listDialog.listBox2.Items.Clear();
			List<VLayerDescription> lyrs = new List<VLayerDescription>();
			foreach(VLayerDescription ld in this.lyrsDescr) {
				if(topic == ld.topic) {
					lyrs.Add(ld);
				}
			}
			this.listDialog.listBox2.ItemsSource = lyrs;
		} // public void changeTopicSelection() {


		public void changeLayerSelection() {
			log("changeLayerSelection...");
			VLayerDescription ld = (VLayerDescription)this.listDialog.listBox2.SelectedItem;
			log("selected layer: " + ld.toString());
		}


		public void addSelectedLayer() {
			log("addSelectedLayer...");
			VLayerDescription ld = (VLayerDescription)this.listDialog.listBox2.SelectedItem;
			if(ld == null) {
				log("no selected layer");
				return;
			}
			log("selected layer: " + ld.toString());
			var lyr = new VLayer(ld);
			// if FeatureLayer retrieve attribs from server
			// look in VLayer.createLayer
			//lr.OutFields.Add("*");
			//lr.Initialize();
			addLayer2Q(lyr); // queue
			//addLayer(lyr);
		} // public void addSelectedLayer()


		public void addLayer(VLayer lyr) {
			log("addLayer " + lyr.lyrUrl);
			ESRI.ArcGIS.Client.Map map = MapApplication.Current.Map;
			ESRI.ArcGIS.Client.LayerCollection lyrs = map.Layers;
			lyr.lyr.InitializationFailed += new EventHandler<EventArgs>(lyr_InitializationFailed);
			lyrs.Add(lyr.lyr);
			MapApplication.SetLayerName(lyr.lyr, lyr.lyrName);
			// http://help.arcgis.com/en/webapps/silverlightviewer/help/index.html#//01770000001s000000
		} // public void addLayer(VLayer lyr) 


		public void addLayer2Q(VLayer lyr) {
			/* add layer to queue; if q.processing in stopped, ping layer service.
			 */
			log("addLayer2Q " + lyr.lyrUrl);
			lyrsQueue.setLock();
			lyrsQueue.Enqueue(lyr);
			if(lyrsQueue.hasProcessor == false) {
				lyrsQueue.hasProcessor = true;
				lyrsQueue.releaseLock();
				pingService(lyr);
				return;
			}
			lyrsQueue.releaseLock();
		} // public void addLayer(VLayer lyr) 


		/// <summary>
		/// Send fake request to layer service, add layer to map in callback
		/// </summary>
		/// <param name="lyr"></param>
		public void pingService(VLayer lyr) {
			var wc = new WebClient();
			wc.OpenReadCompleted += pingsrv_OpenReadCompleted;
			string url = lyr.lyrUrl + "?f=json&pretty=true";
			try {
				Uri u = new Uri(url, UriKind.RelativeOrAbsolute);
				this.listDialog.busyIndicator1.IsBusy = true;
				wc.OpenReadAsync(u, url);
				log(String.Format("pingService, request sent, url [{0}]...", url));
			}
			catch(Exception e) {
				log(String.Format("pingService, catch exception while open url [{0}], msg [{1}]", url, e.Message));
				pingsrv_OpenReadCompleted(null, null);
			}
		} // public void pingService(VLayer lyr) 


		/// <summary>
		/// Layer ping request callback. Peek next layer from queue, call addLayer for current layer
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void pingsrv_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {
			listDialog.busyIndicator1.IsBusy = false;
			log("pingsrv_OpenReadCompleted, got service response");
			var url = e.UserState as string;
			if(e != null && e.Error != null) {
				log(String.Format("pingsrv_OpenReadCompleted, error in answer, "
					+ "url '{2}', msg [{0}], trace [{1}]", e.Error.Message, e.Error.StackTrace, url));
			}
			lyrsQueue.setLock();
			lyrsQueue.hasProcessor = false;
			var lyr = lyrsQueue.Dequeue() as VLayer;
			var next = lyrsQueue.Peek() as VLayer;
			lyrsQueue.releaseLock();
			//e.Result
			addLayer(lyr);
			if(next != null) {
				pingService(next);
			}
		} // void pingsrv_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e) {


		void lyr_InitializationFailed(object sender, EventArgs e) {
			Layer layer = sender as Layer;
			var lyr = new VLayer(layer);
			string msg = "";
			if(layer.InitializationFailure != null) {
				msg = layer.InitializationFailure.ToString() + "\n";
			}
			msg = string.Format(
				"Невозможно включить слой [{0}], \n ошибка [{1}]", lyr.lyrUrl, msg);
			log(string.Format("lyr_InitializationFailed, [{0}]", msg));
			MessageBox.Show(msg);
			layer.Visible = false;
			// remove then add again.
			// this method is not applicable, because IE8 use cache for second request
		} // addSelectedLayer

	} // public class VMSCollection : ICommand, ISupportsConfiguration 

} // namespace mwb02.AddIns 
