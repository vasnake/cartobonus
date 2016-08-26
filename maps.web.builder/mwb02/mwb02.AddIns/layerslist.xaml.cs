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

namespace mwb02.AddIns {
	public partial class layerslist: UserControl {
		public VMSCollection vmsApp;

		/// <summary>
		/// doubleclick emulation, previous click time; previous clicked object
		/// </summary>
		private DateTime lastMLBClick = DateTime.Now;
		object lastClickedObj = null;

		public layerslist() {
			InitializeComponent();
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e) {
			if(this.InputTextBox.Text == "") {
				this.InputTextBox.Text = "URL?";
			}
			addSelectedLayerButton.IsEnabled = false;
		}

		private void onTopicLBSelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				this.vmsApp.log("onTopicLBSelectionChanged...");
				if(e.AddedItems.Count <= 0) {
					this.vmsApp.log("onTopicLBSelectionChanged, sel removed");
					this.vmsApp.fillListDialog();
					return;
				}
				addSelectedLayerButton.IsEnabled = false;
				this.vmsApp.changeTopicSelection();
			}
			catch(Exception ex) { vmsApp.log("onTopicLBSelectionChanged, error: " + ex.Message); }
		} // private void onTopicLBSelectionChanged(object sender, SelectionChangedEventArgs e)


		private void onLayerLBSelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				this.vmsApp.log("onLayerLBSelectionChanged...");
				if(e.AddedItems.Count <= 0) {
					addSelectedLayerButton.IsEnabled = false;
					this.vmsApp.log("onLayerLBSelectionChanged, sel removed");
					return;
				}
				addSelectedLayerButton.IsEnabled = true;
				this.vmsApp.changeLayerSelection();
			}
			catch(Exception ex) { vmsApp.log("onLayerLBSelectionChanged, error: " + ex.Message); }
		} // private void onLayerLBSelectionChanged(object sender, SelectionChangedEventArgs e)


		private void addSelectedLayerButton_Click(object sender, RoutedEventArgs e) {
			try {
				this.vmsApp.log("addSelectedLayerButton_Click...");
				this.vmsApp.addSelectedLayer();
				addSelectedLayerButton.IsEnabled = false;
			}
			catch(Exception ex) { vmsApp.log("addSelectedLayerButton_Click, error: " + ex.Message); }
		}


		/// <summary>
		/// in layers list LBM up event - imitate doubleclick
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void onLayerMLBUp(object sender, MouseButtonEventArgs e) {
			try {
				DateTime n = DateTime.Now;
				var delta = (n - lastMLBClick).TotalMilliseconds;
				lastMLBClick = n;
				if(delta < 500.0 && e.OriginalSource == lastClickedObj) {
					addSelectedLayerButton_Click(sender, null);
				}
				lastClickedObj = e.OriginalSource;
			}
			catch(Exception ex) { vmsApp.log("onLayerMLBUp, error: " + ex.Message); }
		} // private void onLayerMLBUp(object sender, MouseButtonEventArgs e)


		private void imgSource_BindingValidationError(object sender, ValidationErrorEventArgs e) {
			vmsApp.log("imgSource_BindingValidationError, error: " + e.Error.Exception.Message);
		}

		private void bmpBackground_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
			vmsApp.log("bmpBackground_ImageFailed, error: " + e.ErrorException.Message);
			var bi = sender as ImageBrush;
			string a = ((System.Windows.Media.Imaging.BitmapImage)(((bi)).ImageSource)).UriSource.ToString();
			vmsApp.log("bmpBackground_ImageFailed, url " + a);

			var ld = new VLayerDescription();
			Image img = new Image();
			img.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(ld.preview, UriKind.RelativeOrAbsolute));
			bi.ImageSource = img.Source;
		} // private void bmpBackground_ImageFailed(object sender, ExceptionRoutedEventArgs e)

		private void onKeyUp_Control(object sender, KeyEventArgs e) {
			try {
				vmsApp.log(string.Format("onKeyUp_Control, {0}", e.Key)); // Escape
				if(e.Key == System.Windows.Input.Key.Escape) {
					ESRI.ArcGIS.Client.Extensibility.MapApplication.Current.HideWindow(this);
				}
			}
			catch(Exception ex) { vmsApp.log("onKeyUp_Control, error: " + ex.Message); }
		}

	} // public partial class layerslist : UserControl


	/// <summary>
	/// multicolumn panel for listbox
	/// </summary>
	public class MultiColumnPanel: Panel {
		/*
		 http://www.silverlightshow.net/items/Using-the-Border-control-in-Silverlight-2-Beta-1-.aspx
		 http://forums.silverlight.net/t/37536.aspx/1                    
		To use it, you write something like this:
		<ListBox>
		<ListBox.ItemsPanel>
		<ItemsPanelTemplate>
		<local:MultiColumnPanel Columns="3" ColumnWidth="250"/>
		</ItemsPanelTemplate>
		</ListBox.ItemsPanel>
		</ListBox>
		*/

		public static readonly DependencyProperty ColumnsProperty =
			DependencyProperty.Register("Columns", typeof(int), typeof(MultiColumnPanel),
			new PropertyMetadata(1));

		public int Columns {
			get { return (int)this.GetValue(ColumnsProperty); }
			set { this.SetValue(ColumnsProperty, value); }
		}

		public static readonly DependencyProperty ColumnWidthProperty =
			DependencyProperty.Register("ColumnWidth", typeof(double), typeof(MultiColumnPanel),
			new PropertyMetadata(0d));

		public double ColumnWidth {
			get { return (double)this.GetValue(ColumnWidthProperty); }
			set { this.SetValue(ColumnWidthProperty, value); }
		}

		protected override Size MeasureOverride(Size availableSize) {
			Size size = new Size();
			Size finalSize = availableSize;
			finalSize.Height = double.PositiveInfinity;
			int i = 0;
			while(i < this.Children.Count) {
				double tempHeight = 0d; for(int j = 0; j < this.Columns; j++) {
					if((i + j) >= this.Children.Count) {
						break;
					}
					UIElement element = this.Children[i + j];
					if(element != null) {
						element.Measure(availableSize);
						tempHeight = Math.Max(tempHeight, element.DesiredSize.Height);
					}
				}
				size.Height += tempHeight;
				i += this.Columns;
			}
			size.Width = this.ColumnWidth * this.Columns; return size;
		}

		protected override Size ArrangeOverride(Size finalSize) {
			UIElementCollection children = this.Children;
			double heightDelta = 0d;
			int i = 0;
			while(i < this.Children.Count) {
				double tempHeight = 0d; for(int j = 0; j < this.Columns; j++) {
					if((i + j) >= this.Children.Count) {
						break;
					}
					UIElement element = children[i + j];
					if(element != null) {
						Rect finalRect = new Rect(new Point(), finalSize); finalRect.X = j * this.ColumnWidth;
						finalRect.Y += heightDelta;
						tempHeight = Math.Max(tempHeight, element.DesiredSize.Height);
						finalRect.Height = element.DesiredSize.Height;
						finalRect.Width = Math.Max(this.ColumnWidth, element.DesiredSize.Width);
						element.Arrange(finalRect);
					}
				}
				heightDelta += tempHeight;
				i += this.Columns;
			}
			return finalSize;
		}
	} // public class MultiColumnPanel: Panel 

}

