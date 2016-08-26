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
	public partial class VExtraLayersForm: UserControl {
		public VExtraLayersImpl app;
		public void log(string msg) { if(app == null) msg.clog(); else app.log(msg); }

		public VExtraLayersForm() {
			InitializeComponent();
		}

		private void onClickOK(object sender, RoutedEventArgs e) {
			// http://stackoverflow.com/questions/2683891/silverlight-4-default-button-service
			try {
				var lyrtype = this.listBox1.SelectedItem.ToString();
				this.app.userPickLayerType(lyrtype);
			}
			catch(Exception ex) {
				log("onClickOK, error: " + ex.Message);
			}
		}

		private void onClickCancel(object sender, RoutedEventArgs e) {
			try {
				app.cancelAdding();
			}
			catch(Exception ex) {
				log("onClickCancel, error: " + ex.Message);
			}
		}

		private void onSelectionChanged(object sender, SelectionChangedEventArgs e) {
			try {
				log("onSelectionChanged...");
				if(e.AddedItems.Count <= 0) {
					button1.IsEnabled = false; // OK button
					log("onSelectionChanged, sel removed");
					return;
				}
				button1.IsEnabled = true;
			}
			catch(Exception ex) { log("onSelectionChanged, error: " + ex.Message); }
		}
	}
}
