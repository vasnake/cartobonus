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
	public partial class VWMSParametersForm: UserControl {
		public VExtraLayersImpl app;
		public void log(string msg) { if(app == null) msg.clog(); else app.log(msg); }

		public VWMSParametersForm() {
			InitializeComponent();
		}

		private void Add_Click(object sender, RoutedEventArgs e) {
			try {
				this.app.userGiveWMSLayerParams(UrlTextBox.Text, LayersTextBox.Text, VersionTextBox.Text,
					NameTextBox.Text, ProxyTextBox.Text);
			}
			catch(Exception ex) {
				log("WMS Add_Click, error: " + ex.Message);
			}
		}
	}
}
