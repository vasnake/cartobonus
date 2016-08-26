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
	[DisplayName("Print")]
	[Description("Печать")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/print.png")]
	public class VPrintCommand: ICommand, ISupportsConfiguration {

		private MyConfigDialog configDialog = new MyConfigDialog();
		private resizableDebug dbgResizable = new resizableDebug();
		public VPrintImpl vPrintObj = new VPrintImpl();

		/// <summary>
		/// constructor
		/// </summary>
		public VPrintCommand() {
			dbgResizable.ResizeMode = ResizeMode.CanResize;
			dbgResizable.Title = "Отладочные сообщения";
			dbgResizable.textBlock1.Text = "Print log:\n";

			vPrintObj = new VPrintImpl(this.log);
		} // public VPrintCommand()


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
			return MapApplication.Current.Map != null;
		} // public bool CanExecute(object parameter)

		public void Execute(object parameter) {
			log("Execute, ...");
			try {
				dbgResizable.ParentLayoutRoot = (MapApplication.Current.Map.Parent as Grid).Parent as Panel;
				if(dbgResizable.Height < 100) dbgResizable.Height = 100;
				if(dbgResizable.Width < 100) dbgResizable.Width = 100;
				//dbgResizable.Visibility = Visibility.Visible;
				//dbgResizable.Show();
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой вывода окна отладки:\n[{0}]", ex.Message);
				log(msg);
			}

			try {
				vPrintObj.runTool(parameter);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой вызова инструмента 'Печать':\n[{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // public void Execute(object parameter)

		#endregion // #region ICommand members


		public void log(String txt) {
			DateTime dt = DateTime.Now;
			var d = this.dbgResizable.textBlock1;
			var msg = string.Format("VPrintCommand.2012-12-20 {0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			d.Text += msg;
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

	} // public class VPrintCommand: ICommand, ISupportsConfiguration


	public class VPrintImpl: Object {

		public logFunc log;
		private VMapState mapState = VMapState.Unknown;
		private DispatcherTimer timer; // http://blogs.silverlight.net/blogs/msnow/archive/2008/04/01/timers-and-the-main-game-loop.aspx
		public PrintForm frmPrint;

		public void nullLog(string msg) { msg.clog(); }

		public VPrintImpl(logFunc l) {
			log = l;
			configure();
		}
		public VPrintImpl() {
			log = nullLog;
			configure();
		}
		private void configure() {
			// call from constructor
			log(string.Format("VPrintImpl.configure..."));

			if(log == nullLog) { return; }

			// map events, timer, etc.
			try {
				log(string.Format("VPrintImpl.configure, SnapToLevels {0}", MapApplication.Current.Map.SnapToLevels));
				frmPrint = new PrintForm();
				frmPrint.app = this;

				if(timer == null) {
					timer = new DispatcherTimer() {
						Interval = TimeSpan.FromMilliseconds(300)
					};
					timer.Tick += new EventHandler(timer_Tick);
					timer.Start();
				}
				log(string.Format("VPrintImpl.configure done."));
			}
			catch(Exception ex) {
				log(string.Format("VPrintImpl.configure, Initialization failed: {0}", ex.Message));
			}
		} // public void configure()


		void timer_Tick(object sender, EventArgs e) {
			// we need timer because of mapEvents invoke layers reload, which invoke mapEvents and so on
			// http://en.wikipedia.org/wiki/Virtual_finite-state_machine
			VMapState t = mapState;
			try {
				if(mapState == VMapState.Loaded) { // flag set by onLayersInitialized
					log(string.Format("VPrintImpl.timer_Tick, layers Loaded"));
					renewRL(frmPrint.Map);
				}
			}
			catch(Exception ex) {
				mapState = VMapState.Unknown;
				log(string.Format("VPrintImpl.timer_Tick, error: {0}", ex.Message));
			}
			if(t == mapState) {
				mapState = VMapState.Unknown;
			}
		}  // void timer_Tick(object sender, EventArgs e)


		/// <summary>
		/// call from ICommand.Execute
		/// </summary>
		public void runTool(object param) {
			// main 'Print' button pressed
			log("VPrintImpl.runTool ...");

			// clone map
			cloneMap(MapApplication.Current.Map, frmPrint.Map);

			// open print dialog
			MapApplication.Current.ShowWindow("Печать", frmPrint, false,
					evtBeforeHidePrintForm, evtAfterHidePrintForm, WindowType.Floating);
		} // public void runTool(object param)


		private void cloneMap(ESRI.ArcGIS.Client.Map mapFrom, ESRI.ArcGIS.Client.Map mapTo) {
			log("VPrintImpl.cloneMap ...");
			frmPrint.legend1.Map = null;

			var cfg = VSave.mapConfig(mapFrom);
			log(string.Format("cloneMap, mapCfg '{0}'", cfg));

			mapTo.Layers.LayersInitialized -= onLayersInitialized;
			VRestore.loadMapCfg(cfg, mapTo, null, null);
			mapTo.Layers.LayersInitialized += onLayersInitialized;

			frmPrint.legend1.Map = mapTo;
			frmPrint.legend1.ShowOnlyVisibleLayers = true;
			PrintForm.legendFixed = false;
			// going to map update, need time to complete requests...
			//mapTo.ZoomToResolution(mapFrom.Resolution, mapFrom.Extent.GetCenter());
		} // private void cloneMap(ESRI.ArcGIS.Client.Map mapFrom, ESRI.ArcGIS.Client.Map mapTo)


		void onLayersInitialized(object sender, EventArgs args) {
			// call from map event. renew RL after load.from.file
			// set timer for recreate RL layer
			try {
				log(string.Format("VPrintImpl.onLayersInitialized"));

				if(mapState == VMapState.Ready) return;

				if(mapState == VMapState.Processing) // RL refreshed
					mapState = VMapState.Ready;
				else {
					mapState = VMapState.Loaded; // timer will call 'renewRL'
				}
			}
			catch(Exception ex) {
				log(string.Format("VPrintImpl.onLayersInitialized failed, error: {0}", ex.Message));
			}
		} // void onLayersInitialized(object sender, EventArgs args)


		/// <summary>
		/// Reload RL data if RL layer exists.
		/// Called from timer when map layers loaded.
		/// Set mapstate=Processing or Ready
		/// </summary>
		private void renewRL(ESRI.ArcGIS.Client.Map map) {
			// recreate RL layer, load RL content
			if(mapState != VMapState.Loaded) return;
			mapState = VMapState.Processing;
			log(string.Format("VPrintImpl.renewRL"));

			var gl = VRedlineImpl.reloadRLData(map, VRedlineImpl.layerID, VRedlineImpl.layerName);
			if(gl == null) {
				mapState = VMapState.Ready;
				log(string.Format("VPrintImpl.renewRL, graphic layer is null"));
			}

			return;
		} // private void renewRL()


		private void evtBeforeHidePrintForm(object sender, System.ComponentModel.CancelEventArgs args) {
			// Call from Map when HideWindow sequence started.
			// maybe you want cancel hiding window?
			log("evtBeforeHidePrintForm");
			try {
				//args.Cancel = true;
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtBeforeHidePrintForm(object sender, CancelEventArgs args)


		private void evtAfterHidePrintForm(object sender, EventArgs args) {
			// Call from Map when window hided.
			log("evtAfterHidePrintForm");
			try {
				this.frmPrint.resetLegend();
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtAfterHidePrintForm(object sender, EventArgs args)


	} // public class VPrintImpl: Object 


} // namespace mwb02.AddIns
