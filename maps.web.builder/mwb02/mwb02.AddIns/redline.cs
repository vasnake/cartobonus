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
	[DisplayName("Redline")]
	[Description("Пометки на карте")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/redline.png")] // http://www.iconfinder.com/icondetails/47538/128/gps_location_maps_marker_icon
	public class VRedlineCommand: ICommand, ISupportsConfiguration {

		private MyConfigDialog configDialog = new MyConfigDialog();
		private resizableDebug dbgResizable = new resizableDebug();

		public VRedlineImpl vrlObj = new VRedlineImpl();

		/// <summary>
		/// constructor
		/// </summary>
		public VRedlineCommand() {
			dbgResizable.ResizeMode = ResizeMode.CanResize;
			dbgResizable.Title = "отладочные сообщения";
			dbgResizable.textBlock1.Text = "Redline log:\n";

			vrlObj = new VRedlineImpl(this.log);
		} // public VRedlineCommand()


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

		public bool CanExecute(object parameter) {
			return MapApplication.Current.Map != null;
		} // public bool CanExecute(object parameter)

		public event EventHandler CanExecuteChanged;

		public void Execute(object parameter) {
			log("Redline.Execute, ...");
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
				vrlObj.runTool(parameter);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой вызова инструмента 'Пометки':\n[{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}

		} // public void Execute(object parameter)

		#endregion // #region ICommand members


		public void log(String txt) {
			var ver = "20130222";
			var dt = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
			var msg = string.Format("{0} Redline.{1} {2}\n", dt, ver, txt);
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)

	} // public class VRedlineCommand: ICommand, ISupportsConfiguration


	public class VRedlineImpl: VAddonCommand {

		//public logFunc log;
		public Draw draw;
		public VPseudoWindow frmElement = new VPseudoWindow();
		public RLAttribsForm frmAttribs = new RLAttribsForm();
		public static string layerID = "rlLocalLayer";
		public static string layerName = "Пометки";
		public static ESRI.ArcGIS.Client.Geometry.Geometry currGeom;
		public Graphic currMark;
		public static string markType; // DisplayName in ResourceDictionary
		public VFlags markState = VFlags.Unknown;
		private DispatcherTimer timer; // http://blogs.silverlight.net/blogs/msnow/archive/2008/04/01/timers-and-the-main-game-loop.aspx
		private VMapState mapState = VMapState.Unknown;
		private FrameworkElement rlToolbar;

		/// <summary>
		/// GraphicSource for GraphicLayer
		/// </summary>
		private ESRI.ArcGIS.Client.GraphicCollection marks = new GraphicCollection();

		public static Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>
			dicSymbols = new Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>();
		public static Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>
			dicLineSymbols = new Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>();
		public static Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>
			dicAreaSymbols = new Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>();
		public static Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>
			dicTextSymbols = new Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol>();

		public void nullLog(string msg) { msg.clog(); }

		public VRedlineImpl(logFunc l) {
			log = l;
			configure();
		}
		public VRedlineImpl() {
			log = nullLog;
			configure();
		}


		private void configure() {
			// call from constructor
			log(string.Format("VRedlineImpl.configure..."));
			frmElement.app = this;
			frmAttribs.app = this;

			// symbols lists
			try {
				initSymbolsDicts();
			}
			catch(Exception ex) {
				log(string.Format("VRedlineImpl.configure, symbols dicts failed: {0}", ex.Message));
			}

			if(log == nullLog) { return; }

			// map events, timer, etc.
			try {
				MapApplication.Current.Map.Layers.LayersInitialized -= Layers_LayersInitialized;
				MapApplication.Current.Map.Layers.LayersInitialized += Layers_LayersInitialized;
				MapApplication.Current.Map.MouseMove -= Map_MouseMove;
				MapApplication.Current.Map.MouseMove += Map_MouseMove;
				if(timer == null) {
					timer = new DispatcherTimer() {
						Interval = TimeSpan.FromMilliseconds(300)
					};
					timer.Tick += timer_Tick;
					timer.Start();
				}
			}
			catch(Exception ex) {
				log(string.Format("VRedlineImpl.configure, set LayersInitialized failed: {0}", ex.Message));
			}
		} // public void configure()


		private void initSymbolsDicts() {
			// call from constructor
			// markers
			var ls = frmAttribs.Resources["listSymbols"] as ObjectCollection;
			dicSymbols.Clear();
			foreach(var sym in ls) {
				var ifs = sym as ESRI.ArcGIS.Mapping.Core.Symbols.ImageFillSymbol;
				var n = ifs.DisplayName;
				dicSymbols.Add(n, ifs);
				//пропавшее нашлось в c:\Inetpub\wwwroot\Apps\app4\Viewer.xap\ESRI.ArcGIS.Mapping.Core.dll
				//var vtn = sym as VTwoNames;
				//var symb = frmAttribs.Resources[vtn.keyName] as PictureMarkerSymbol;
				//var n = vtn.DisplayName;
				//dicSymbols.Add(n, symb);
			}

			// lines
			ls = frmAttribs.Resources["listLineSymbols"] as ObjectCollection;
			dicLineSymbols.Clear();
			foreach(var sym in ls) {
				var vtn = sym as VTwoNames;
				//var sls = frmAttribs.Resources[vtn.keyName] as SimpleLineSymbol;
				var sls = frmAttribs.Resources[vtn.keyName] as LineSymbol;
				var n = vtn.DisplayName;
				dicLineSymbols.Add(n, sls);
			}

			// polygons
			ls = frmAttribs.Resources["listAreaSymbols"] as ObjectCollection;
			dicAreaSymbols.Clear();
			foreach(var sym in ls) {
				var vtn = sym as VTwoNames;
				//var sfs = frmAttribs.Resources[vtn.keyName] as SimpleFillSymbol;
				var sfs = frmAttribs.Resources[vtn.keyName] as FillSymbol;
				var n = vtn.DisplayName;
				dicAreaSymbols.Add(n, sfs);
			}

			ls = frmAttribs.Resources["listTextSymbols"] as ObjectCollection;
			dicTextSymbols.Clear();
			foreach(var sym in ls) {
				var vtn = sym as VTwoNames;
				var ts = frmAttribs.Resources[vtn.keyName] as TextSymbol;
				var n = vtn.DisplayName;
				dicTextSymbols.Add(n, ts);
			}
		} // private void initSymbolsDicts()


		void timer_Tick(object sender, EventArgs e) {
			// we need timer because of mapEvents invoke layers reload, which invoke mapEvents and so on
			// http://en.wikipedia.org/wiki/Virtual_finite-state_machine
			VMapState t = mapState;
			try {
				if(mapState == VMapState.Loaded) { // flag set by Layers_LayersInitialized
					log(string.Format("VRedlineImpl.timer_Tick, Loaded"));
					renewRL();
				}
			}
			catch(Exception ex) {
				mapState = VMapState.Unknown;
				log(string.Format("VRedlineImpl.timer_Tick, error: {0}", ex.Message));
			}
			if(t == mapState) {
				mapState = VMapState.Unknown;
			}
		}  // void timer_Tick(object sender, EventArgs e)


		/// <summary>
		/// call from ICommand.Execute
		/// </summary>
		public void runTool(object param) {
			// main 'RL' button pressed
			log("VRedlineImpl.runTool, ...");
			// todo: if currMark in work and Form opened - do nothing

			// show toolbar
			if(rlToolbar == null) {
				log("runTool, init rlToolbar");
				rlToolbar = MapApplication.Current.FindObjectInLayout("VGraphicsTools") as FrameworkElement;
				if(rlToolbar == null) {
					throw new Exception("Не найден UI элемент 'VGraphicsTools',\n"
						+ "вероятно, используется неоригинальная версия программы");
				}
				// init tbEvents in first time
				//http://forums.arcgis.com/threads/44838-ToolBar-Deprecated-at-V2.3
				var tb = MapApplication.Current.FindObjectInLayout("VGraphicsToolbar") as ESRI.ArcGIS.Client.Toolkit.Toolbar;
				tb.ToolbarItemClicked += new ESRI.ArcGIS.Client.Toolkit.ToolbarIndexChangedHandler(VGR_ToolbarItemClicked);
				tb.ToolbarIndexChanged += new ESRI.ArcGIS.Client.Toolkit.ToolbarIndexChangedHandler(VGR_ToolbarIndexChanged);
			}
			rlToolbar.Visibility = Visibility.Visible;

			// show rl layer
			var gl = getRLLayer();
			log("runTool, layer created");

			// init Draw
			if(draw == null) {
				log("runTool, create Draw");
				draw = new Draw(MapApplication.Current.Map) {
					LineSymbol = dicLineSymbols["Черновая линия"] as LineSymbol,
					FillSymbol = dicAreaSymbols["Черновой полигон"] as FillSymbol
				};
				draw.DrawComplete += evtDrawComplete;
				// Listen to the IsEnabled property.
				// This is to detect cases where other tools have
				// disabled the Draw surface. Close dialog window
				Utils.RegisterForNotification("IsEnabled", draw, frmElement, evtOnDrawEnabledChanged);
			}
			log("runTool, done.");
		} // public void runTool(object param)


		private void VGR_ToolbarItemClicked(object sender, ESRI.ArcGIS.Client.Toolkit.SelectedToolbarItemArgs e) {
			log(string.Format("VGR_ToolbarItemClicked, run by index: [{0}]", e.Index));
			rlToolbar.Visibility = Visibility.Collapsed;
			draw.IsEnabled = true;
			switch(e.Index) {
				case 0: // point
					draw.DrawMode = DrawMode.Point;
					markType = "Флажок";
					break;
				case 1: // line
					draw.DrawMode = DrawMode.Polyline;
					markType = "Полилиния";
					break;
				case 2: // area
					draw.DrawMode = DrawMode.Polygon;
					markType = "Полигон";
					break;
				case 3: // textsymbol
					draw.DrawMode = DrawMode.Point;
					markType = "Текст";
					break;
				default: // close tool
					draw.DrawMode = DrawMode.None;
					draw.IsEnabled = false;
					markType = "";
					break;
			} // end switch
		} // private void VGR_ToolbarItemClicked(object sender, ESRI.ArcGIS.Client.Toolkit.SelectedToolbarItemArgs e) {


		private void VGR_ToolbarIndexChanged(object sender, ESRI.ArcGIS.Client.Toolkit.SelectedToolbarItemArgs e) {
			//StatusTextBlock.Text = e.Item.Text;
			log(string.Format("VGR_ToolbarIndexChanged, selected type: [{0}]", e.Item.Text));
		} // private void VGR_ToolbarIndexChanged(object sender, ESRI.ArcGIS.Client.Toolkit.SelectedToolbarItemArgs e)


		/// <summary>
		/// Fires when the Draw surface is enabled or disabled
		/// for hide dialog window if Draw disabled
		/// or for show tooltip like 'RL disabled, another tool is working'
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
		private void evtOnDrawEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var wnd = d as VPseudoWindow;
			var app = wnd.app as VRedlineImpl;
			app.log(string.Format("VRedlineImpl.evtOnDrawEnabledChanged, draw enabled? {0}", app.draw.IsEnabled));

			if(e.OldValue != null && !(bool)e.NewValue &&
				!wnd.disabledDrawInternally) {
				//MapApplication.Current.HideWindow(wnd);
				wnd.Visibility = Visibility.Collapsed;
				app.rlToolbar.Visibility = Visibility.Collapsed;
			}
			else if(wnd.disabledDrawInternally)
				wnd.disabledDrawInternally = false;
		} // evtOnDrawEnabledChanged


		/// <summary>
		/// Call from Map when user stop draw current mark.
		/// Example says:
		/// ESRI.ArcGIS.Client.Graphic graphic = new ESRI.ArcGIS.Client.Graphic()
		/// { Geometry = args.Geometry, Symbol = _activeSymbol };
		/// graphicsLayer.Graphics.Add(graphic);
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void evtDrawComplete(object sender, DrawEventArgs e) {
			log(string.Format("VRedlineImpl.evtDrawComplete, SR={0}", e.Geometry.SpatialReference.WKID));
			currGeom = e.Geometry;
			markState = VFlags.New;
			frmElement.disabledDrawInternally = true;
			draw.DrawMode = DrawMode.None;
			draw.IsEnabled = false;

			try {
				//init form
				currMark = null;
				frmAttribs.IsEnabled = false;

				frmAttribs.tbID.Text = Guid.NewGuid().ToString();
				frmAttribs.tbName.Text = "Название";
				frmAttribs.tbDescr.Text = "Описание";
				// symbols
				setFormMarkType();

				frmAttribs.IsEnabled = true;

				// new mark from form data
				currMark = newMark();
				// add mark to layer
				setSelectedSymbol();

				//showMarkForm
				MapApplication.Current.ShowWindow("Атрибуты пометки", frmAttribs, false,
					evtBeforeHideAttrForm, evtAfterHideAttrForm, WindowType.Floating);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой создания маркера: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtDrawComplete(object sender, DrawEventArgs e)


		private void setFormMarkType() {
			// setup symbols combobox in form using currGeom, markType
			ObservableCollection<VSymbol> oc = null;
			if(isText()) {
				oc = makeVSymbolOC(dicTextSymbols);
			}
			else if(isPoint(currGeom)) {
				oc = makeVSymbolOC(dicSymbols);
			}
			else if(isLine(currGeom)) {
				oc = makeVSymbolOC(dicLineSymbols);
			}
			else if(isArea(currGeom)) {
				oc = makeVSymbolOC(dicAreaSymbols);
			}
			else { throw new Exception("setFormMarkType: Неизвестный тип геометрии"); }
			//frmAttribs.cbType.DataContext = oc;
			frmAttribs.cbType.ItemsSource = oc;
			frmAttribs.cbType.SelectedItem = getItem(oc, markType);
		} // private void setFormMarkType()


		private void showMarkForm() {
			// call from 'editMarker'
			// fill form from mark, open form
			log("showMarkForm");
			if(currMark == null) {
				log("currMark is null");
				return;
			}

			frmAttribs.IsEnabled = false;
			var mark = currMark;
			currMark = null;

			frmAttribs.tbID.Text = mark.Attributes["aID"] as string;
			frmAttribs.tbName.Text = mark.Attributes["aName"] as string;
			frmAttribs.tbDescr.Text = mark.Attributes["aDescr"] as string;

			markType = mark.Attributes["aType"] as string;
			currGeom = mark.Geometry;
			setFormMarkType();

			currMark = mark;
			frmAttribs.IsEnabled = true;

			MapApplication.Current.ShowWindow("Атрибуты пометки", frmAttribs, false,
				evtBeforeHideAttrForm, evtAfterHideAttrForm, WindowType.Floating);
		} // private void showMarkForm()


		/// <summary>
		/// create and init new marker from form data
		/// </summary>
		/// <returns></returns>
		public Graphic newMark() {
			log(string.Format("newMark, id=[{0}]", frmAttribs.tbID.Text));

			var mark = new Graphic() {
				Geometry = currGeom
			};
			mark.Attributes.Add("aID", clone(frmAttribs.tbID.Text));
			mark.Attributes.Add("aName", clone(frmAttribs.tbName.Text));
			mark.Attributes.Add("aDescr", clone(frmAttribs.tbDescr.Text));

			var vs = frmAttribs.cbType.SelectedItem as VSymbol;
			mark.Attributes.Add("aType", clone(vs.DisplayName));

			mark.Symbol = vs.symbol;

			if(isText()) {
				mark.Symbol = remakeTextSymbol(mark);
			}
			return mark;
		} // public Graphic newMark()


		/// <summary>
		/// Replace current marker by recreated Graphic.
		/// Call from form event 'onSelectSymbol' or 'evtDrawComplete' or other 'ChangedMarkAttribs' events
		/// </summary>
		public void setSelectedSymbol() {
			if(currMark == null) {
				log("setSelectedSymbol, currMark is null");
				return;
			}
			log("setSelectedSymbol, replace graphic");

			marks.Remove(currMark);
			currMark = null;
			currMark = newMark();
			marks.Add(currMark);
			log("setSelectedSymbol, new mark added");

			// refresh RL layer
			var gl = getRLLayer();
			gl.Graphics.Clear();
			if(marks.Count <= 1) // reattach RLL for attributes table initialization
				MapApplication.Current.Map.Layers.Remove(gl);
			gl.Graphics = new GraphicCollection(marks);
			addRLL2Map(gl);
		} // public void setSelectedSymbol()


		public void saveCurrentMark() {
			// call from window.save button
			log("VRedlineImpl.saveCurrentMark");
			setSelectedSymbol(); // write attribs to Graphic item
			markState = VFlags.Saved;

			MapApplication.Current.HideWindow(frmAttribs);
		} // public void saveCurrentMark()


		/// <summary>
		/// Remove mark from layer;
		/// call from RLAttribsForm.remove button
		/// </summary>
		public void removeCurrentMark() {
			log("VRedlineImpl.removeCurrentMark");
			if(currMark == null || markState == VFlags.Deleted) {
				log("currMark is null");
				return;
			}
			var gl = getRLLayer();
			gl.Graphics.Remove(currMark);
			marks.Remove(currMark);
			markState = VFlags.Deleted;

			MapApplication.Current.HideWindow(frmAttribs);
		} // public void removeCurrentMark()


		private void evtBeforeHideAttrForm(object sender, System.ComponentModel.CancelEventArgs args) {
			// Call from Map when HideWindow sequence started.
			// maybe you want cancel hiding window?
			log("evtBeforeHideAttrForm");
			try {
				//args.Cancel = true;
				//frmAttribs.gridTextAttr.DataContext = null;
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtBeforeHideAttrForm(object sender, CancelEventArgs args)


		private void evtAfterHideAttrForm(object sender, EventArgs args) {
			// Call from Map when window hided.
			log("evtAfterHideAttrForm");
			try {
				if(markState == VFlags.New) {
					removeCurrentMark();
				}
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой закрытия окна: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void evtAfterHideAttrForm(object sender, EventArgs args)


		/// <summary>
		/// Return existing RLL or create new RL graphics layer and add it to map
		/// </summary>
		/// <returns>Redline layer</returns>
		private GraphicsLayer getRLLayer() {
			log("VRedlineImpl.getRLLayer");
			mapState = VMapState.Processing;

			//var gl = makeRLLayer(MapApplication.Current.Map, layerID, layerName);
			var gl = MapApplication.Current.Map.Layers[layerID] as GraphicsLayer;
			if(gl != null) return gl;

			gl = createRLLayer(layerID, layerName);
			gl = addRLL2Map(gl);
			return gl;
		} // private GraphicsLayer getRLLayer()


		/// <summary>
		/// Add RL layer to map or return already existing layer
		/// </summary>
		/// <param name="rl"></param>
		/// <returns></returns>
		private GraphicsLayer addRLL2Map(GraphicsLayer gl) {
			log(string.Format("VRedlineImpl.addRLL2Map, initialize and add to map RL layer"));
			var el = MapApplication.Current.Map.Layers[gl.ID] as GraphicsLayer;
			if(el != null) {
				log(string.Format("VRedlineImpl.addRLL2Map, layer already exist"));
				return el;
			}

			//var lyr = MapApplication.Current.SelectedLayer;

			gl.MouseLeftButtonDown -= gl_MouseLeftButtonDown;
			gl.MouseLeftButtonDown += gl_MouseLeftButtonDown;
			gl.Initialize();
			MapApplication.Current.Map.Layers.Add(gl);
			MapApplication.SetLayerName(gl, layerName);

			//MapApplication.Current.SelectedLayer = lyr;
			//ESRI.ArcGIS.Client.Extensibility.LayerProperties.SetIsPopupEnabled(gl, false);
			return gl;
		} // private GraphicsLayer addRLL2Map(GraphicsLayer rl)


		/// <summary>
		/// If RL layer exists reload RL data and reattach RLL to map.
		/// Called from timer when map layers loaded.
		/// Set mapstate=Processing or Ready
		/// </summary>
		private void renewRL() {
			log(string.Format("VRedlineImpl.renewRL"));
			if(mapState != VMapState.Loaded) return;
			mapState = VMapState.Processing;

			var lyr = MapApplication.Current.SelectedLayer;

			//load RL content
			var gl = reloadRLData(MapApplication.Current.Map, VRedlineImpl.layerID, VRedlineImpl.layerName);
			if(gl == null) {
				mapState = VMapState.Ready;
				log(string.Format("VRedlineImpl.renewRL, RL layer doesn't exists"));
				return;
			}
			log(string.Format("VRedlineImpl.renewRL, symbols restored"));

			// attach RL layer to map in right way
			marks = new GraphicCollection(gl.Graphics);
			MapApplication.Current.Map.Layers.Remove(gl);
			addRLL2Map(gl);

			if(gl.ID == lyr.ID) MapApplication.Current.SelectedLayer = gl;
			log(string.Format("VRedlineImpl.renewRL done"));
			return;
		} // private void renewRL()


		/// <summary>
		/// Reload graphics and restore graphics symbols in RL layer
		/// </summary>
		/// <param name="map">Map</param>
		/// <param name="layerID">layer id</param>
		/// <param name="layerName">layer name</param>
		/// <returns>redline layer or null</returns>
		public static GraphicsLayer reloadRLData(Map map, string layerID, string layerName) {
			// recreate RL layer, load RL content
			var gl = map.Layers[layerID] as GraphicsLayer;
			if(gl == null) {
				string.Format("VRedlineImpl.reloadRLData, lyr '{0}' doesn't exist", layerID).clog();
				return null;
			}

			var rlc = VLayer.getContent(gl);
			//map.Layers.Remove(gl);
			//gl = makeRLLayer(map, layerID, layerName);
			gl.Graphics.Clear();
			restoreRLGraphics(gl, rlc);
			return gl;
		} // public static GraphicsLayer reloadRLData(Map map, string layerID, string layerName)


		/// <summary>
		/// Create graphics from xml and restore Symbol for each Graphic in layer
		/// </summary>
		/// <param name="gl">redline layer</param>
		/// <param name="xmlContent">symbols parameters</param>
		public static void restoreRLGraphics(GraphicsLayer gl, string xmlContent) {
			// set Graphics symbols
			gl = VLayer.setContent(gl, xmlContent);

			foreach(var gr in gl.Graphics) {
				var t = gr.Attributes["aType"] as string;
				ESRI.ArcGIS.Client.Geometry.Geometry g = gr.Geometry;

				if(isText(g, t)) { gr.Symbol = remakeTextSymbol(gr); }
				else if(isPoint(g)) { gr.Symbol = dicSymbols[t]; }
				else if(isLine(g)) { gr.Symbol = dicLineSymbols[t]; }
				else if(isArea(g)) { gr.Symbol = dicAreaSymbols[t]; }
				else {
					(string.Format("restoreRLGraphics, unknown Geom type [{0}]", g.GetType())).clog();
					continue;
				}
			}
		} // public static void restoreRLGraphics(GraphicsLayer gl, string xmlContent)


		/// <summary>
		/// Create and add to map a simple graphics layer or return existed
		/// </summary>
		/// <returns>redline layer</returns>
		public static GraphicsLayer makeRLLayer(Map map, string layerID, string layerName) {
			return VLayer.makeRLLayer(map, layerID, layerName);
		} // public static GraphicsLayer makeRLLayer(Map map, string layerID, string layerName)


		/// <summary>
		/// Create RL layer.
		/// </summary>
		/// <param name="layerID"></param>
		/// <param name="layerName"></param>
		/// <returns></returns>
		public static GraphicsLayer createRLLayer(string layerID, string layerName) {
			return VLayer.makeRLLayer(null, layerID, layerName);
		} // public static GraphicsLayer createRLLayer(Map map, string layerID, string layerName)


		void Map_MouseMove(object sender, MouseEventArgs e) {
			// print coordinates
			try {
				//log(string.Format("VRedlineImpl.Map_MouseMove"));
				var c = MapApplication.Current;
				var ScreenCoordsTextBlock = c.FindObjectInLayout("ScreenCoordsTextBlock") as TextBlock;
				var MapCoordsTextBlock = c.FindObjectInLayout("MapCoordsTextBlock") as TextBlock;

				if(c.Map.Extent != null) {
					System.Windows.Point screenPoint = e.GetPosition(c.Map);
					ScreenCoordsTextBlock.Text = string.Format("Screen Coords: X = {0}, Y = {1}",
						screenPoint.X, screenPoint.Y);

					ESRI.ArcGIS.Client.Geometry.MapPoint mapPoint = c.Map.ScreenToMap(screenPoint);
					if(c.Map.WrapAroundIsActive) {
						mapPoint = ESRI.ArcGIS.Client.Geometry.Geometry.NormalizeCentralMeridian(mapPoint) as ESRI.ArcGIS.Client.Geometry.MapPoint;
					}
					MapCoordsTextBlock.Text = string.Format("X = {0}, Y = {1}",
						Math.Round(mapPoint.X, 4), Math.Round(mapPoint.Y, 4));

					var wm = new ESRI.ArcGIS.Client.Projection.WebMercator();
					mapPoint = wm.ToGeographic(mapPoint) as ESRI.ArcGIS.Client.Geometry.MapPoint;
					ScreenCoordsTextBlock.Text = string.Format("Lon = {0}, Lat = {1}",
						Math.Round(mapPoint.X, 7), Math.Round(mapPoint.Y, 7));
				}
			}
			catch(Exception ex) {
				log(string.Format("VRedlineImpl.Map_MouseMove, error: {0}", ex.Message));
			}
		} // void Map_MouseMove(object sender, MouseEventArgs e) {


		/// <summary>
		/// On map.LayersInitialized event. Set mapstate=Loaded for renew RLL task
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		void Layers_LayersInitialized(object sender, EventArgs args) {
			// call from map event. renew RL after load.from.file
			// set timer for recreate RL layer
			try {
				log(string.Format("VRedlineImpl.Layers_LayersInitialized"));
				var gl = MapApplication.Current.Map.Layers[layerID] as GraphicsLayer;
				if(gl == null) return;

				if(mapState == VMapState.Loaded) return;
				if(mapState == VMapState.Processing) {
					mapState = VMapState.Ready;
					return;
				}

				mapState = VMapState.Loaded; // timer will call 'renewRL'
				return;
			}
			catch(Exception ex) {
				log(string.Format("VRedlineImpl.Layers_LayersInitialized, RL init error: {0}", ex.Message));
			}
		}  // void Layers_LayersInitialized(object sender, EventArgs args)


		void gl_MouseLeftButtonDown(object sender, GraphicMouseButtonEventArgs e) {
			// call from map LB event
			try {
				log(string.Format("gl_MouseLeftButtonDown, continue draw or open popup wnd?"));
				if(this.draw != null && this.draw.IsEnabled != false) {
					e.Handled = false;
					log(string.Format("gl_MouseLeftButtonDown, make drawing - not popup"));
					return;
				}
				var gr = e.Graphic;
				if(gr == null) {
					log(string.Format("gl_MouseLeftButtonDown, e.Graphic is null, nothing to do"));
					return;
				}
				log(string.Format("gl_MouseLeftButtonDown, open popup"));
				e.Handled = true; // timer business? LOG: System.NullReferenceException: Object reference not set to an instance of an object. at ESRI.ArcGIS.Mapping.Controls.Utils.EditorCommandUtility.StopEditing()
				editMarker(gr);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой редактирования пометки: \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // void gl_MouseLeftButtonDown(object sender, GraphicMouseButtonEventArgs e)


		private void editMarker(Graphic gr) {
			// call from 'gl_MouseLeftButtonDown'
			//log(string.Format("editMarker"));
			var aType = gr.Attributes["aType"];
			var aID = gr.Attributes["aID"];
			log(string.Format("editMarker, type=[{0}], id=[{1}]", aType, aID));

			currGeom = gr.Geometry;
			currMark = gr;
			markState = VFlags.Saved;
			showMarkForm();
		} // private void editMarker(Graphic gr)


		private void restoreRLLayer(GraphicsLayer gl, string xmlContent) {
			// set Graphics symbols
			log("restoreRLLayer, ...");
			gl = VLayer.setContent(gl, xmlContent);

			foreach(var g in gl.Graphics) {
				var typ = g.Attributes["aType"] as string;
				currGeom = g.Geometry;
				markType = typ;

				if(isText()) { g.Symbol = remakeTextSymbol(g); }
				else if(isPoint(g.Geometry)) { g.Symbol = dicSymbols[typ]; }
				else if(isLine(g.Geometry)) { g.Symbol = dicLineSymbols[typ]; }
				else if(isArea(g.Geometry)) { g.Symbol = dicAreaSymbols[typ]; }
				else {
					log(string.Format("unknown Geom type [{0}]", g.Geometry.GetType()));
					continue;
				}

				currGeom = null;
				markType = null;
			}
		} // private void restoreRLLayer(GraphicsLayer gl, string xmlContent)


		public string getRLContent() {
			log("getRLContent(), ...");
			var gl = getRLLayer();
			return getRLContent(gl);
		} // public string getRLContent()


		public string getRLContent(GraphicsLayer gl) {
			// serialize GraphicsLayer
			log("getRLContent(gl), ...");
			return VLayer.getContent(gl);
		} // public string getRLContent(GraphicsLayer gl)


		public static TextSymbol remakeTextSymbol(Graphic g) {
			var typ = g.Attributes["aType"] as string;
			var symb = clone(dicTextSymbols[typ] as TextSymbol);

			symb.Text = string.Format("{0}\n{1}",
				g.Attributes["aName"], g.Attributes["aDescr"]);
			return symb;
		} // private TextSymbol remakeTextSymbol(Graphic g)


		public static string clone(string s) {
			return string.Format("{0}", s);
		}

		public static TextSymbol clone(TextSymbol s) {
			var symb = new TextSymbol();
			symb.Text = clone(s.Text);
			symb.FontFamily = new FontFamily(s.FontFamily.ToString());
			symb.FontSize = s.FontSize;
			symb.Foreground = new SolidColorBrush((s.Foreground as SolidColorBrush).Color);
			symb.OffsetX = s.OffsetX;
			symb.OffsetY = s.OffsetY;
			return symb;
		}

		private bool isText() {
			// markType contains 'текст' and currGeom is point
			return VRedlineImpl.isText(currGeom, markType);
		}
		public static bool isText(ESRI.ArcGIS.Client.Geometry.Geometry g, string type) {
			// markType contains 'текст' and currGeom is point
			if(isPoint(g))
				if(type.ToUpper().Contains("Текст".ToUpper()))
					return true;
			return false;
		}
		public static bool isPoint(ESRI.ArcGIS.Client.Geometry.Geometry g) {
			if(g.GetType() == typeof(ESRI.ArcGIS.Client.Geometry.MapPoint))
				return true;
			return false;
		}
		public static bool isLine(ESRI.ArcGIS.Client.Geometry.Geometry g) {
			if(g.GetType() == typeof(ESRI.ArcGIS.Client.Geometry.Polyline))
				return true;
			return false;
		}
		public static bool isArea(ESRI.ArcGIS.Client.Geometry.Geometry g) {
			if(g.GetType() == typeof(ESRI.ArcGIS.Client.Geometry.Polygon))
				return true;
			return false;
		}


		private ObservableCollection<VSymbol> makeVSymbolOC(
			Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol> symbDict) {
			var res = new ObservableCollection<VSymbol>();
			foreach(KeyValuePair<string, ESRI.ArcGIS.Client.Symbols.Symbol> kvp in symbDict) {
				var vs = new VSymbol();
				vs.DisplayName = kvp.Key;
				vs.symbol = kvp.Value;
				res.Add(vs);
			}
			return res;
		} // private ObservableCollection<VSymbol> makeVSymbolOC(Dictionary<string, ESRI.ArcGIS.Client.Symbols.Symbol> symbDict)


		private VSymbol getItem(ObservableCollection<VSymbol> oc, string key) {
			VSymbol res = null; ;
			foreach(var x in oc) {
				if(x.DisplayName.Equals(key)) {
					res = x;
					break;
				}
			}
			return res;
		} // private VSymbol getItem(ObservableCollection<VSymbol> oc, string key)

	} // public class VRedlineImpl


	public class VAddonCommand: Object {
		public logFunc log;
	} // public class VAddonCommand: Object


	public class VPseudoWindow: FrameworkElement {
		// used in Map events processing
		public VAddonCommand app;
		internal bool disabledDrawInternally = false;
	} // public class VPseudoWindow: FrameworkElement


	public enum VFlags: int {
		Unknown = 0,
		New = 1,
		Saved = 2,
		Deleted = 3,
		Updated = 4
	} // public enum VFlags: int


	public enum VMapState: int {
		Unknown = 0,
		Loaded = 1,
		Processing = 2,
		Ready = 3
	} // public enum VMapState: int

} // namespace mwb02.AddIns
