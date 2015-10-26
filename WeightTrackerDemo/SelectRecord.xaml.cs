// Copyright (c) Microsoft Corp.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using Microsoft.Phone.Controls;

using CHBase.SDK.Mobile;

namespace WeightTrackerDemo

{
    public partial class SelectRecord : PhoneApplicationPage
    {
        public SelectRecord()
        {
            InitializeComponent();

            Loaded += new RoutedEventHandler(SelectRecord_Loaded);
            c_RecordListBox.SelectionChanged += new SelectionChangedEventHandler(c_RecordListBox_SelectionChanged);
        }

        void c_RecordListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            App.HealthVaultService.CurrentRecord = App.HealthVaultService.Records[c_RecordListBox.SelectedIndex];
            App.HealthVaultService.SaveSettings(MainPage.SettingsFilename);

            NavigationService.GoBack();
        }

        void SelectRecord_Loaded(object sender, RoutedEventArgs e)
        {
            c_RecordListBox.Items.Clear();

            foreach (CHBaseRecord record in App.HealthVaultService.Records)
            {
                ListBoxItem item = new ListBoxItem();
                TextBlock textBlock = new TextBlock();
                textBlock.Text = record.RecordName;
                textBlock.Margin = new Thickness(0, 15, 0, 15);
                item.Content = textBlock;

                c_RecordListBox.Items.Add(item);
            }
        }
    }
}
