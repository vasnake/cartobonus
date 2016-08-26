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
using System.Collections.ObjectModel;

namespace QueryRelatedRecords.AddIns {
	public partial class VShortList: UserControl {
		private QueryRelatedTool app = null;
		private DateTime lastMLBClick;

		public VShortList(QueryRelatedTool p) {
			app = p;
			InitializeComponent();
			_relationsList = new ObservableCollection<mwb02.AddIns.VRelationInfo>();
			this.DataContext = this;
			lastMLBClick = DateTime.Now;
			MouseLeftButtonUp += new MouseButtonEventHandler(onMLBUp);
		}


		#region Relations List
		private ObservableCollection<mwb02.AddIns.VRelationInfo> _relationsList;
		public ObservableCollection<mwb02.AddIns.VRelationInfo> relationsList {
			get { return _relationsList; }
		}
		#endregion Relations List


		private void button1_Click(object sender, RoutedEventArgs e) {
			app.log("VShortList, user hit 'OK'");
			ESRI.ArcGIS.Client.Extensibility.MapApplication.Current.HideWindow(app.relationsListForm);
		}


		/// <summary>
		/// MouseLeftButtonUp += new MouseButtonEventHandler(onMLBUp);
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void onMLBUp(object sender, MouseButtonEventArgs e) {
			DateTime n = DateTime.Now;
			var delta = (n - lastMLBClick).TotalMilliseconds;
			lastMLBClick = n;
			app.log(string.Format("VShortList.onMLBUp, delta is {0}", delta));

			if(delta < 500.0) {
				app.log(string.Format("VShortList.onMLBUp, doubleclick, delta is {0}", delta));
				button1_Click(sender, null);
			}
		} // private void onMLBUp(object sender, MouseButtonEventArgs e)
	}
}
