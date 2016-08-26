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

    public partial class RLAttribsForm: UserControl {

        public VRedlineImpl app;

        public RLAttribsForm() {
            InitializeComponent();
        }


        private void saveButton_Click(object sender, RoutedEventArgs e) {
            try {
                app.log("saveButton_Click");
                app.saveCurrentMark();
            } catch(Exception ex){
                string msg = string.Format("Сбой сохранения: \n [{0}]", ex.Message);
                MessageBox.Show(msg);
                app.log(msg);
            }
        } // private void saveButton_Click(object sender, RoutedEventArgs e)


        private void removeButton_Click(object sender, RoutedEventArgs e) {
            try {
                app.log("removeButton_Click");
                app.removeCurrentMark();
            }
            catch(Exception ex) {
                string msg = string.Format("Сбой удаления: \n [{0}]", ex.Message);
                MessageBox.Show(msg);
                app.log(msg);
            }
        } // private void removeButton_Click(object sender, RoutedEventArgs e)


        private void onSelectSymbol(object sender, SelectionChangedEventArgs e) {
            try {
                app.log("onSelectSymbol, combobox");
                if(e.AddedItems.Count <= 0) {
                    app.log("onSelectSymbol, selection removed");
                    return;
                }
                app.setSelectedSymbol(); // onSelectSymbol
            }
            catch(Exception ex) {
                string msg = string.Format("Сбой выбора символа: \n [{0}]", ex.Message);
                MessageBox.Show(msg);
                app.log(msg);
            }
        } // private void onSelectSymbol(object sender, SelectionChangedEventArgs e)


        private void onNameChanged(object sender, TextChangedEventArgs e) {
            try {
                app.log("onNameChanged");
                //app.setTextAttrib();
                app.markState = VFlags.Updated;
            }
            catch(Exception ex) {
                string msg = string.Format("Сбой изменения названия: \n [{0}]", ex.Message);
                app.log(msg);
            }
        } // private void onNameChanged(object sender, TextChangedEventArgs e)


        private void onDescrChanged(object sender, TextChangedEventArgs e) {
            try {
                app.log("onDescrChanged");
                //app.setTextAttrib();
                app.markState = VFlags.Updated;
            }
            catch(Exception ex) {
                string msg = string.Format("Сбой изменения описания: \n [{0}]", ex.Message);
                app.log(msg);
            }
        } // private void onDescrChanged(object sender, TextChangedEventArgs e)

    } // public partial class RLAttribsForm: UserControl

} // namespace mwb02.AddIns
