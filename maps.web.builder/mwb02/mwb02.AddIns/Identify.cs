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
//using System.Text.RegularExpressions;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Extensibility;
using ESRI.ArcGIS.Client.Actions;
using ESRI.ArcGIS.Client.Symbols;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Tasks;
using ESRI.ArcGIS.Client.Toolkit;


namespace mwb02.AddIns {
	/// <summary>
	/// http://resources.esri.com/help/9.3/arcgisserver/apis/silverlight/help/Identify_task.htm
	/// </summary>
	[Export(typeof(ICommand))]
	[DisplayName("Identify")]
	[Description("Features attributes")]
	[Category("CGIS Tools")]
	[DefaultIcon("/mwb02.AddIns;component/Images/identify.png")] // http://www.iconfinder.com/search/?q=information
	public class VIdentify: ICommand, ISupportsConfiguration {
		private MyConfigDialog configDialog = new MyConfigDialog();
		private debug dbgDialog = new debug();
		private resizableDebug dbgResizable = new resizableDebug();

		//private IdentifyWnd identifyDialog;
		private resizableIdentify identifyDialog = new resizableIdentify();
		private IdentifyTask identifyTask;
		private Draw draw;
		internal ObservableCollection<DataItem>
			DataItems { get; set; }
		string srvUrl = "";

		/// <summary>
		/// constructor
		/// </summary>
		public VIdentify() {
			//dbgDialog.textBlock1.Text = "Identify log:\n";
			dbgResizable.textBlock1.Text = "Identify log:\n";

			DataItems = new ObservableCollection<DataItem>();
			//identifyDialog = new IdentifyWnd()
			//identifyDialog.Margin = new Thickness(10),
			identifyDialog.DataContext = DataItems;
			identifyDialog.app = this;
			identifyDialog.Closed += new EventHandler(identifyDialog_Closed);


			srvUrl = "http://rngis.algis.com/ArcGIS/rest/services/SeismicNavigat/MapServer"; // "http://sampleserver1.arcgisonline.com/ArcGIS/rest/services/Demographics/ESRI_Census_USA/MapServer";
			identifyTask =
				new IdentifyTask(srvUrl);
			identifyTask.ExecuteCompleted += IdentifyTask_ExecuteCompleted;
			identifyTask.Failed += IdentifyTask_Failed;
		} // public VIdentify()


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
			dbgResizable.ParentLayoutRoot = (MapApplication.Current.Map.Parent as Grid).Parent as Panel;
			dbgResizable.ResizeMode = ResizeMode.CanResize;
			if(dbgResizable.Height < 100) dbgResizable.Height = 100;
			if(dbgResizable.Width < 100) dbgResizable.Width = 100;
			dbgResizable.Title = "отладочные сообщения";
			//dbgResizable.Visibility = Visibility.Visible;
			//dbgResizable.Show();
			//MapApplication.Current.ShowWindow("Debug messages", dbgDialog, false);

			log("VIdentify.Execute, ...");

			if(draw == null) {
				draw = new Draw(MapApplication.Current.Map) { DrawMode = ESRI.ArcGIS.Client.DrawMode.Point };
				draw.DrawComplete += DrawComplete;

				// Listen to the IsEnabled property.
				// This is to detect cases where other tools have
				// disabled the Draw surface.
				Utils.RegisterForNotification("IsEnabled", draw, identifyDialog, OnDrawEnabledChanged);
			}
			draw.DrawMode = DrawMode.Point;
			draw.IsEnabled = true;

			//MapApplication.Current.ShowWindow("Identify", identifyDialog, false, null, IdentifyDialogHidden);
			identifyDialog.ParentLayoutRoot = (MapApplication.Current.Map.Parent as Grid).Parent as Panel;
			identifyDialog.DataDisplayTitleBottom.Text = "Щелкните в интересующей точке карты";
			identifyDialog.IdentifyResultsPanel.Visibility = Visibility.Collapsed;
			identifyDialog.ResizeMode = ResizeMode.CanResize;
			if(identifyDialog.Height < 100) identifyDialog.Height = 100;
			if(identifyDialog.Width < 100) identifyDialog.Width = 100;
			identifyDialog.Title = "Атрибутика";
			identifyDialog.Visibility = Visibility.Visible;
			identifyDialog.Show();
		} // public void Execute(object parameter)

		#endregion // #region ICommand members


		public void log(String txt) {
			DateTime dt = DateTime.Now;
			//var d = this.dbgDialog.textBlock1;
			var d = this.dbgResizable.textBlock1;
			var msg = string.Format("{0} {1}\n", dt.ToString("yyyy-MM-dd hh:mm:ss"), txt);
			d.Text += msg;
			msg.clog();
			System.Diagnostics.Debug.WriteLine(txt);
		} // public void log(String txt)


		/// <summary>
		/// Fires when the drawing action is complete.
		/// Issues an identify operation using the drawn geometry.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DrawComplete(object sender, DrawEventArgs e) {
			log("VIdentify.DrawComplete");
			var map = MapApplication.Current.Map;
			var clickPoint = e.Geometry as MapPoint;

			var identifyParams = new IdentifyParameters() {
				Geometry = clickPoint.Clone(),
				MapExtent = map.Extent.Clone(),
				LayerOption = LayerOption.visible,
				Tolerance = 2, // default = 2
				SpatialReference = map.SpatialReference.Clone(),
				Height = (int)map.ActualHeight,
				Width = (int)map.ActualWidth,
				ReturnGeometry = false
			};

			// get layer url
			/*
			 * для линейных слоев иногда ничего не находит
			 * Возможно потому, что надо выбирать толеранец поболе?
			 * http://rngis.algis.com/ArcGIS/rest/services/mesh/MapServer/identify?geometryType=esriGeometryPoint&geometry=%7b%22x%22%3a8678329.72666139%2c%22y%22%3a6793256.24367089%2c%22spatialReference%22%3a%7b%22wkid%22%3a102100%7d%7d&returnGeometry=true&sr=102100&imageDisplay=0%2c0%2c96&layers=visible&tolerance=2&mapExtent=-7380211.5%2c2988666.0221519%2c12657297%2c12246502.2278481&f=json&
			 */
			var lyr = new VLayer(MapApplication.Current.SelectedLayer);
			srvUrl = lyr.getAGSMapServiceUrl();
			if(srvUrl == "") {
				log(string.Format("VIdentify.DrawComplete, layer has wrong type [{0}]", lyr.lyrType));
				identifyDialog.DataDisplayTitleBottom.Text =
					string.Format("Ошибка, выделенный слой [тип {0}] не поддерживает операцию Identify, ", lyr.lyrType);
				return;
			}
			identifyTask.Url = srvUrl;
			identifyTask.ProxyURL = lyr.proxy;
			//identifyTask.Token = "";
			log(string.Format("VIdentify.DrawComplete, ask [{0}]", srvUrl));

			// clear result window
			DataItems.Clear();
			// say "wait for server..."
			identifyDialog.DataDisplayTitleBottom.Text = "Ждем ответа от сервера...";

			if(identifyTask.IsBusy)
				identifyTask.CancelAsync();
			identifyTask.ExecuteAsync(identifyParams);

			var graphicsLayer = map.Layers["IdentifyResultsLayer"]
				as GraphicsLayer;
			if(graphicsLayer == null) {
				graphicsLayer = createResultsLayer();
				map.Layers.Add(graphicsLayer);
				MapApplication.Current.SelectedLayer = lyr.lyr;
			}
			else {
				graphicsLayer.ClearGraphics();
			}

			Graphic graphic = new Graphic() { Geometry = clickPoint };
			graphicsLayer.Graphics.Add(graphic);
		} // private void DrawComplete(object sender, DrawEventArgs e)


		/// <summary>
		/// Fires when the identify operation has completed successfully.
		/// Updates the data shown in the identify dialog.
		/// http://resources.esri.com/help/9.3/arcgisserver/apis/silverlight/help/Identify_task.htm
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void IdentifyTask_ExecuteCompleted(object sender, IdentifyEventArgs args) {
			log("VIdentify.IdentifyTask_ExecuteCompleted");
			DataItems.Clear();
			var title = identifyDialog.DataDisplayTitleBottom;
			title.Text = "Ничего не найдено";
			identifyDialog.IdentifyResultsPanel.Visibility = Visibility.Collapsed;

			// shrink grid and restore column.width options later
			var dg = identifyDialog.IdentifyDetailsDataGrid;
			dg.ColumnWidth = DataGridLength.SizeToCells;
			foreach(var col in dg.Columns) {
				col.Width = DataGridLength.SizeToHeader; // DataGridLength.Auto;
			}

			// fill datagrid with results
			if(args.IdentifyResults != null && args.IdentifyResults.Count > 0) {
				title.Text = string.Format("Найдено записей: {0}",
					args.IdentifyResults.Count);
				identifyDialog.IdentifyResultsPanel.Visibility = Visibility.Visible;

				foreach(IdentifyResult result in args.IdentifyResults) {
					Graphic feature = result.Feature;
					DataItems.Add(new DataItem() {
						Title = (result.Value.ToString() + " (" + result.LayerName + ")").Trim(),
						Data = feature.Attributes
					});
				} // foreach result

				// update grid layout
				dg.ColumnWidth = DataGridLength.Auto;
				foreach(var col in dg.Columns) {
					col.Width = DataGridLength.Auto;
				}
				identifyDialog.UpdateLayout();
				identifyDialog.IdentifyComboBox.SelectedIndex = 0;
			} // if has results           
		} // private void IdentifyTask_ExecuteCompleted(object sender, IdentifyEventArgs args)

		private void IdentifyTask_Failed(object sender, TaskFailedEventArgs e) {
			log("VIdentify.IdentifyTask_Failed: " + e.Error);
			identifyDialog.DataDisplayTitleBottom.Text = "Identify failed. Error: " + e.Error;
			MessageBox.Show("Identify failed. Error: " + e.Error);
		}


		/// <summary>
		/// Fires when the identify dialog is closed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void IdentifyDialogHidden(object sender, EventArgs e) {
			log("VIdentify.IdentifyDialogHidden");
			var map = MapApplication.Current.Map;

			DataItems.Clear();
			identifyDialog.IdentifyResultsPanel.Visibility = Visibility.Collapsed;
			identifyDialog.disabledDrawInternally = true;

			map.Layers.Remove(map.Layers["IdentifyResultsLayer"]);
			draw.DrawMode = DrawMode.None;
			draw.IsEnabled = false;
		} // private void IdentifyDialogHidden(object sender, EventArgs e)
		void identifyDialog_Closed(object sender, EventArgs e) {
			IdentifyDialogHidden(sender, e);
		}


		/// <summary>
		/// Fires when the Draw surface is enabled or disabled
		/// </summary>
		/// <param name="d"></param>
		/// <param name="e"></param>
		private static void OnDrawEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			//var dlg = d as IdentifyWnd;
			var dlg = d as resizableIdentify;
			dlg.app.log("VIdentify.OnDrawEnabledChanged");

			if(e.OldValue != null && !(bool)e.NewValue && !dlg.disabledDrawInternally) {
				//MapApplication.Current.HideWindow(dlg);
				dlg.Visibility = Visibility.Collapsed;
			}
			else if(dlg.disabledDrawInternally)
				dlg.disabledDrawInternally = false;
		} // private static void OnDrawEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)


		/// <summary>
		/// Creates a simple graphics layer to show the location identified
		/// </summary>
		/// <returns></returns>
		private GraphicsLayer createResultsLayer() {
			log("VIdentify.createResultsLayer, create new GraphicsLayer");

			var gl = new GraphicsLayer() {
				ID = "IdentifyResultsLayer",
				Renderer = new SimpleRenderer() {
					//Symbol = identifyDialog.Resources["RedMarkerSymbol"] as Symbol
					Symbol = identifyDialog.LayoutRoot.Resources["RedMarkerSymbol"] as Symbol
				}
			};

			// Set layer name in Map Contents
			// gl.SetValue(MapApplication.LayerNameProperty, "Выборка атрибутики"); // http://forums.arcgis.com/threads/51206-Adding-WMS-Service?p=178500&viewfull=1#post178500
			MapApplication.SetLayerName(gl, "Выборка атрибутики");
			return gl;
		} // private GraphicsLayer createResultsLayer()

	} // public class VIdentify : ICommand, ISupportsConfiguration


	public class DataItem {
		public string Title { get; set; }
		public IDictionary<string, object> Data { get; set; }
	} // public class DataItem


	public class Utils {
		public static void RegisterForNotification(
			string propertyName, object source,
			FrameworkElement element, PropertyChangedCallback callback) {
			//Bind to a depedency property
			Binding b = new Binding(propertyName) { Source = source };
			var prop = System.Windows.DependencyProperty.RegisterAttached(
				"ListenAttached" + propertyName,
				typeof(object),
				typeof(UserControl),
				new PropertyMetadata(callback));

			if(element != null) element.SetBinding(prop, b);
		}
	} // public class Utils

} // namespace CGIS.VS.Addins
