using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Data;

using System.Windows.Printing;
using System.ComponentModel;

using System.Collections.ObjectModel;

using ESRI.ArcGIS.Client;
using ESRI.ArcGIS.Client.Geometry;
using ESRI.ArcGIS.Client.Extensibility;


namespace mwb02.AddIns {
	public partial class PrintForm: UserControl {

		public VPrintImpl app;
		public void log(string msg) { if(app == null) msg.clog(); else app.log(msg); }
		public static bool legendFixed = false;

		public PrintForm() {
			InitializeComponent();

			//http://help.arcgis.com/en/webapi/silverlight/apiref/ESRI.ArcGIS.Client.Toolkit~ESRI.ArcGIS.Client.Toolkit.Legend.html
			legend1.Map = MapApplication.Current.Map;
			legend1.LayerItemsMode = ESRI.ArcGIS.Client.Toolkit.Legend.Mode.Flat;

			scaleLine1.Map = MapApplication.Current.Map;

			_fontFamilies = new ObservableCollection<FontFamily>();

			FontFamilies.Add(new FontFamily("Portable User Interface"));
			FontFamilies.Add(new FontFamily("Arial"));
			FontFamilies.Add(new FontFamily("Arial Black"));
			FontFamilies.Add(new FontFamily("Arial Unicode MS"));
			FontFamilies.Add(new FontFamily("Calibri"));
			FontFamilies.Add(new FontFamily("Cambria"));
			FontFamilies.Add(new FontFamily("Cambria Math"));
			FontFamilies.Add(new FontFamily("Comic Sans MS"));
			FontFamilies.Add(new FontFamily("Candara"));
			FontFamilies.Add(new FontFamily("Consolas"));
			FontFamilies.Add(new FontFamily("Constantia"));
			FontFamilies.Add(new FontFamily("Corbel"));
			FontFamilies.Add(new FontFamily("Courier New"));
			FontFamilies.Add(new FontFamily("Georgia"));
			FontFamilies.Add(new FontFamily("Lucida Grande"));
			FontFamilies.Add(new FontFamily("Segoe UI"));
			FontFamilies.Add(new FontFamily("Symbol"));
			FontFamilies.Add(new FontFamily("Tahoma"));
			FontFamilies.Add(new FontFamily("Times New Roman"));
			FontFamilies.Add(new FontFamily("Trebuchet MS"));
			FontFamilies.Add(new FontFamily("Verdana"));

			_SelectedPaperX = 290; // 210;
			_SelectedPaperY = 200; // 297;
			updatePixelsFromMillimeters();

			this.DataContext = this;
		}


		/// <summary>
		/// Title font family
		/// </summary>
		#region Font Family

		private ObservableCollection<FontFamily> _fontFamilies;
		public ObservableCollection<FontFamily> FontFamilies {
			get { return _fontFamilies; }
		}

		public FontFamily _selectedFontFamily = new FontFamily("Arial");
		public FontFamily SelectedFontFamily {
			get { return _selectedFontFamily; }
			set {
				_selectedFontFamily = value;
				ChangeFontFamily();
				//RaisePropertyChanged("SelectedFontFamily");
			}
		}

		public void ChangeFontFamily() {
			log(string.Format("ChangeFontFamily '{0}'", SelectedFontFamily.Source));
			//EventManager.FireFontFamilyShapeChanged(this, SelectedFontFamily);
		}

		#endregion Font Family


		/// <summary>
		/// Title & Descr font size
		/// </summary>
		#region Font Sizes

		public double _selectedFontSize = 24;
		public double SelectedFontSize {
			get { return _selectedFontSize; }
			set {
				_selectedFontSize = value;
				ChangeFontSize();
				//RaisePropertyChanged("SelectedFontSize");
			}
		}

		public double _selectedDescrFontSize = 16;
		public double SelectedDescrFontSize {
			get { return _selectedDescrFontSize; }
			set {
				_selectedDescrFontSize = value;
				ChangeFontSize();
				//RaisePropertyChanged("SelectedDescrFontSize");
			}
		}

		public void ChangeFontSize() {
			log(string.Format("ChangeFontSize '{0}', '{1}'", SelectedFontSize, SelectedDescrFontSize));
			//EventManager.FireFontSizeShapeChanged(this, SelectedFontSize);
		}

		#endregion Font Sizes


		/// <summary>
		/// Paper size, selected by user
		/// </summary>
		#region Paper size

		public int _SelectedPaperX;
		public int _SelectedPaperY;
		public int SelectedPaperX {
			get { return _SelectedPaperX; }
			set {
				_SelectedPaperX = value;
				ChangePaperSize();
				//RaisePropertyChanged("SelectedPaperX");
			}
		}
		public int SelectedPaperY {
			get { return _SelectedPaperY; }
			set {
				_SelectedPaperY = value;
				ChangePaperSize();
				//RaisePropertyChanged("SelectedPaperY");
			}
		}

		public void ChangePaperSize() {
			log(string.Format("ChangePaperSize '{0}'x'{1}'", SelectedPaperX, SelectedPaperY));
			//printableArea.Visibility = System.Windows.Visibility.Collapsed;
			//EventManager.FireFontSizeShapeChanged(this, SelectedFontSize);
		}

		#endregion Paper size


		/// <summary>
		/// Printable area size in pixels, calc from paper size
		/// </summary>
		#region Image size

		public int _SelectedWidthPx;
		public int _SelectedHeightPx;
		public int SelectedWidthPx {
			get { return _SelectedWidthPx; }
			set {
				_SelectedWidthPx = value;
				ChangeImageSize();
				//RaisePropertyChanged("SelectedWidthPx");
			}
		}
		public int SelectedHeightPx {
			get { return _SelectedHeightPx; }
			set {
				_SelectedHeightPx = value;
				ChangeImageSize();
				//RaisePropertyChanged("SelectedHeightPx");
			}
		}

		public void ChangeImageSize() {
			log(string.Format("ChangeImageSize '{0}'x'{1}'", SelectedWidthPx, SelectedHeightPx));
			//EventManager.FireFontSizeShapeChanged(this, SelectedFontSize);
		}

		#endregion Image size


		private void updatePixelsFromMillimeters() {
			// Calc printableArea size to fit in SelectedPaperX, SelectedPaperY
			// screen mm = paper mm = pixelsize/dpm
			/*
			 * Width="750" Height="1000" ~= A4 210 x 297
			 * 96 dpi = 96 / 25.4 dpm = 3.78 dpm
			 */
			double dpm = 3.78;
			SelectedWidthPx = (int)((double)SelectedPaperX * dpm);
			SelectedHeightPx = (int)((double)SelectedPaperY * dpm);
			log(string.Format("updatePixelsFromMillimeters, you want size {0}x{1} mm, you get size {2}x{3} px",
				SelectedPaperX, SelectedPaperY, SelectedWidthPx, SelectedHeightPx));
		} // private void updatePixelsFromMillimeters()


		private void onClickPrint(object sender, RoutedEventArgs e) {
			try {
				log(string.Format("onClickPrint, numlayers {0}, ext {1}",
					Map.Layers.Count, Map.Extent.ToString()));

				//app.doPrint();
				var doc = new PrintDocument();
				doc.PrintPage += new EventHandler<PrintPageEventArgs>(doc_PrintPage);
				doc.Print("Веб-карта");
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой печати (onClickPrint): \n [{0}]", ex.Message);
				MessageBox.Show(msg);
				log(msg);
			}
		} // private void onClickPrint(object sender, RoutedEventArgs e)


		private void doc_PrintPage(object sender, PrintPageEventArgs e) {
			// Говорим принтеру, что необходимая область печати - заданный элемент PrintArea
			try {
				log("doc_PrintPage");
				e.PageVisual = this.printableArea;
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой печати (doc_PrintPage): \n [{0}]", ex.Message);
				//MessageBox.Show(msg);
				log(msg);
			}
		} // private void doc_PrintPage(object sender, PrintPageEventArgs e)


		private void onMapLabelChanged(object sender, TextChangedEventArgs e) {
			try {
				// change map label
				//this.label1.Content = string.Format("{0}", this.tbMapLabel.Text);
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой смены заголовка (onMapLabelChanged): \n [{0}]", ex.Message);
				log(msg);
			}
		} // private void onMapLabelChanged(object sender, TextChangedEventArgs e)


		public void updateLayout() {
			// apply print layout
			log(string.Format("PrintForm.updateLayout"));
			this.label1.Content = string.Format("{0}", this.tbMapLabel.Text);
			this.label1.FontFamily = SelectedFontFamily;
			this.label1.FontSize = SelectedFontSize;

			this.tbDescription.Text = string.Format("{0}", tbDescrSrc.Text);
			this.tbDescription.FontFamily = SelectedFontFamily;
			this.tbDescription.FontSize = SelectedDescrFontSize;

			updatePixelsFromMillimeters();
			printableArea.Height = SelectedHeightPx;
			printableArea.Width = SelectedWidthPx;

			scaleLine1.Map = this.Map;
			scaleLine1.MapUnit = ESRI.ArcGIS.Client.Toolkit.ScaleLine.ScaleLineUnit.Kilometers;

			if(legendFixed) { ; }
			else {
				legendFixed = true;
				ESRI.ArcGIS.Client.Toolkit.Legend lgnd = this.legend1;
				foreach(var x in lgnd.LayerItems) { // remove basemap from legend
					lgnd.LayerItems.Remove(x);
					break;
				}

				ObservableCollection<ESRI.ArcGIS.Client.Toolkit.Primitives.LayerItemViewModel>
					swap = new ObservableCollection<ESRI.ArcGIS.Client.Toolkit.Primitives.LayerItemViewModel>();
				foreach(var x in lgnd.LayerItems) { // fix labels
					var vl = new VLayer(x.Layer);
					x.Label = vl.lyrName;
					swap.Add(x);
				}
				lgnd.LayerItems.Clear();
				for(int ind = swap.Count; ind > 0; ind--) {
					var x = swap[ind - 1];
					lgnd.LayerItems.Insert(swap.Count - ind, x);
				}
			} // fix legend

			if(cbLegend.IsChecked == false) {
				// legend off
				this.legend1.Visibility = System.Windows.Visibility.Collapsed;
			}
			else {
				// legend on
				this.legend1.Visibility = System.Windows.Visibility.Visible;
				// legend width tailored
				log(string.Format("PrintForm.updateLayout: legend W {0}, map W {1}, prnA W {2}; legend ActW {3}, map ActW {4}",
					legend1.Width, Map.Width, printableArea.Width, legend1.ActualWidth, Map.ActualWidth));
				double mw = this.Map.ActualWidth;
				if(double.IsNaN(mw)) {
					mw = printableArea.Width;
				}
				if(double.IsNaN(this.legend1.ActualWidth) ||
					this.legend1.ActualWidth > (mw / 4)) {
					this.legend1.Width = mw / 4;
				}
			} // show legend

			if(cbComment.IsChecked == false) {
				// comment off
				this.tbDescription.Visibility = System.Windows.Visibility.Collapsed;
			}
			else {
				// comment on
				this.tbDescription.Visibility = System.Windows.Visibility.Visible;
			}
		} // public void updateLayout()

		public void resetLegend() {
			this.legend1.Width = double.NaN;
		}

		private void onClickApplyButton(object sender, RoutedEventArgs e) {
			try {
				updateLayout();
			}
			catch(Exception ex) {
				string msg = string.Format("Сбой обновления макета: \n [{0}]", ex.Message);
				log(msg);
			}
		} // private void onClickApplyButton(object sender, RoutedEventArgs e)


	} // public partial class PrintForm: UserControl
} // namespace mwb02.AddIns
