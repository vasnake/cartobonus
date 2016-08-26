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
    public partial class resizableIdentify: FloatableWindow {
        public VIdentify app = null;
        internal bool disabledDrawInternally = false;
        public resizableIdentify() {
            InitializeComponent();
        }

        private void onDataGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
            try {
                var si = (sender as DataGrid).SelectedItem;
                var kvp = (KeyValuePair<string, object>)si;
                var val = kvp.Value as string;
                app.log(string.Format("onDataGridSelectionChanged, val='{2}', selitem='{0}', type='{1}'",
                    si, si.GetType(), val));
                //onDataGridSelectionChanged, selitem='[НазваниеСкважины, Приразломная 4]', type='System.Collections.Generic.KeyValuePair`2[System.String,System.Object]'
            }
            catch(Exception ex) {
                string msg = string.Format("Сбой обработки атрибута: \n [{0}]", ex.Message);
                MessageBox.Show(msg);
                app.log(msg);
            }
        }
    }
}
