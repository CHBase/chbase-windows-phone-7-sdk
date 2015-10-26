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
#define UseHostedBrowser
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
using Microsoft.Phone.Tasks;
using System.Windows.Navigation;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using System.IO;

namespace WeightTrackerDemo
{
    public partial class MainPage : PhoneApplicationPage
    {
        public const string SettingsFilename = "Settings.xml";
        bool _addingRecord;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            Loaded += new RoutedEventHandler(MainPage_Loaded);
            c_NewApplication.Click += new RoutedEventHandler(c_NewApplication_Click);
            c_AddRecord.Click += new RoutedEventHandler(c_AddRecord_Click);
            c_SaveWeight.Click += new RoutedEventHandler(c_SaveWeight_Click);
            c_ClearWeight.Click += new RoutedEventHandler(c_ClearWeight_Click);
            c_RecordButton.Click += new RoutedEventHandler(c_RecordButton_Click);

            WebTransport.RequestResponseLogEnabled = true;

            c_RecordPanel.Opacity = 0.0;
        }

        void c_RecordButton_Click(object sender, RoutedEventArgs e)
        {
            c_RecordPanel.Opacity = 0;
            c_Response.Text = "";

            Uri pageUri = new Uri("/SelectRecord.xaml", UriKind.RelativeOrAbsolute);
            NavigationService.Navigate(pageUri);
        }

        void c_AddRecord_Click(object sender, RoutedEventArgs e)
        {
            _addingRecord = true;
            App.HealthVaultService.BeginAuthorizeRecords(AuthenticationCompleted, DoShellAuthentication);
        }

        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.HealthVaultService.LoadSettings(SettingsFilename);
            App.HealthVaultService.BeginAuthenticationCheck(AuthenticationCompleted, DoShellAuthentication);
            SetProgressBarVisibility(true);
        }

        void c_NewApplication_Click(object sender, RoutedEventArgs e)
        {
            // force restart of the auth process...
            App.HealthVaultService.ClearProvisioningInformation();
            _addingRecord = false;

            App.HealthVaultService.BeginAuthenticationCheck(AuthenticationCompleted, DoShellAuthentication);
            SetProgressBarVisibility(true);
        }

        void DoShellAuthentication(object sender, CHBaseResponseEventArgs e)
        {
            SetProgressBarVisibility(false);

            App.HealthVaultService.SaveSettings(SettingsFilename);

            string url;

            if (_addingRecord)
            {
                url = App.HealthVaultService.GetUserAuthorizationUrl();
            }
            else
            {
                url = App.HealthVaultService.GetApplicationCreationUrl();
            }

            App.HealthVaultShellUrl = url;

#if !UseHostedBrowser
            Uri pageUri = new Uri("/HealthVaultIntroPage.xaml", UriKind.RelativeOrAbsolute);

            Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    NavigationService.Navigate(pageUri);
                });
#else
            WebBrowserTask task = new WebBrowserTask();
            task.URL = HttpUtility.UrlEncode(url);
            task.Show();
#endif
        }

        void SetProgressBarVisibility(bool visible)
        {
            Dispatcher.BeginInvoke(() =>
            {
                c_progressBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        void SetRecordName(string recordName)
        {
            Dispatcher.BeginInvoke(() =>
            {
                c_RecordName.Text = recordName;
                c_RecordImage.Source = null;
            });
        }

        void AuthenticationCompleted(object sender, CHBaseResponseEventArgs e)
        {
            SetProgressBarVisibility(false);

            if (e != null && e.ErrorText != null)
            {
                SetRecordName(e.ErrorText);
                return;
            }

            if (App.HealthVaultService.CurrentRecord == null)
            {
                App.HealthVaultService.CurrentRecord = App.HealthVaultService.Records[0];
            }

            App.HealthVaultService.SaveSettings(SettingsFilename);
            if (App.HealthVaultService.CurrentRecord != null)
            {
                SetRecordName(App.HealthVaultService.CurrentRecord.RecordName);

                //GetThingsStart();
                GetPersonalImageStart();
            }
        }


        string GetDateTime(DateTime dateTime, string outerNodeName)
        {
            XElement outerNode =
                new XElement(outerNodeName,
                    new XElement("date",
                        new XElement("y", dateTime.Year),
                        new XElement("m", dateTime.Month),
                        new XElement("d", dateTime.Day)
                    ),
                    new XElement("time",
                        new XElement("h", dateTime.Hour),
                        new XElement("m", dateTime.Minute),
                        new XElement("s", dateTime.Second),
                        new XElement("f", dateTime.Millisecond)
                    )
                );

            return outerNode.ToString();
        }

        void c_SaveWeight_Click(object sender, RoutedEventArgs e)
        {
            string thingXml = @"<info><thing>
                <type-id>3d34d87e-7fc1-4153-800f-f56592cb0d17</type-id>
                <thing-state>Active</thing-state>
                <flags>0</flags>
                <data-xml>
                    <weight>
                        {0}
                        <value>
                            <kg>{1}</kg>
                            <display units=""pounds"">{2}</display>
                        </value>
                    </weight>
                    <common/>
                </data-xml>
            </thing></info>";

            double weight = Double.Parse(c_textWeight.Text);
            c_textWeight.Text = "";
            string whenString = GetDateTime(DateTime.Now, "when");
            string xml = String.Format(thingXml, whenString, weight / 2.204, weight);

            XElement info = XElement.Parse(xml);

            CHBaseRequest request = new CHBaseRequest("PutThings", "2", info, PutThingsCompleted);

            App.HealthVaultService.BeginSendRequest(request);
            c_progressBar.Visibility = Visibility.Visible;
        }

        void PutThingsCompleted(object sender, CHBaseResponseEventArgs e)
        {
            if (e.ErrorText != null)
            {
                // handle error...
            }

            SetProgressBarVisibility(false);

            XElement response = XElement.Parse(e.ResponseXml);

            XElement thingIdNode = response.Descendants("thing-id").Single();

            Guid thingId = new Guid(thingIdNode.Value);

            Guid versionStamp = new Guid(thingIdNode.Attribute("version-stamp").Value);

            GetThingsStart();
        }

        void GetThingsStart()
        {
            string thingXml = @"        <info>
            <group>
                <filter>
                    <type-id>3d34d87e-7fc1-4153-800f-f56592cb0d17</type-id>
                    <thing-state>Active</thing-state>
                </filter>
                <format>
                    <section>core</section>
                    <xml/>
                    <type-version-format>3d34d87e-7fc1-4153-800f-f56592cb0d17</type-version-format>
                </format>
            </group>
        </info>";

            XElement info = XElement.Parse(thingXml);

            CHBaseRequest request = new CHBaseRequest("GetThings", "3", info, GetThingsCompleted);

            App.HealthVaultService.BeginSendRequest(request);
            SetProgressBarVisibility(true);
        }

        List<XElement> GetThingsFromResponse(CHBaseResponseEventArgs eventArgs)
        {
            XElement responseNode = XElement.Parse(eventArgs.ResponseXml);

            XElement infoNode = responseNode.Element(XName.Get("info", "urn:com.microsoft.wc.methods.response.GetThings3"));

            List<XElement> thingNodes = new List<XElement>(infoNode.Element("group").Elements("thing"));

            return thingNodes;
        }

        List<string> _currentThingIds = new List<string>();

        void GetThingsCompleted(object sender, CHBaseResponseEventArgs e)
        {
            SetProgressBarVisibility(false);

            if (e.ErrorText == null)
            {
                _currentThingIds.Clear();

                List<XElement> thingNodes = GetThingsFromResponse(e);

                List<string> values = new List<string>();
                int count = 0;
                foreach (XElement thingNode in thingNodes)
                {
                    XElement thingIdNode = thingNode.Element("thing-id");
                    _currentThingIds.Add(thingIdNode.ToString());

                    XElement dateNode = thingNode.Element("eff-date");
                    DateTime dateTime = DateTime.Parse(dateNode.Value);
                    string value, units = "kg";
                    try
                    {
                        XElement displayValueNode = thingNode.Descendants("display").Single();
                        value = displayValueNode.Value;
                        units = displayValueNode.Attribute("units").Value;
                    }
                    catch
                    {
                        XElement displayValueNode = thingNode.Descendants("value").Single();
                        value = displayValueNode.Descendants("kg").Single().Value;
                    }

                    string displayValue = dateTime.ToString() + "      " + value + " " + units;

                    values.Add(displayValue);

                    count++;
                    if (count == 10)
                    {
                        break;
                    }
                }

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    string text = "Results\r\n";

                    foreach (string value in values)
                    {
                        text += value + "\r\n";
                    }

                    c_Response.Text = text;
                });
            }
        }

        void c_ClearWeight_Click(object sender, RoutedEventArgs e)
        {
            XElement info = new XElement("info");

            foreach (string thingIdText in _currentThingIds)
            {
                info.Add(XElement.Parse(thingIdText));
            }

            CHBaseRequest request = new CHBaseRequest("RemoveThings", "1", info, RemoveThingsCompleted);

            App.HealthVaultService.BeginSendRequest(request);
            SetProgressBarVisibility(true);
        }

        void RemoveThingsCompleted(object sender, CHBaseResponseEventArgs e)
        {
            SetProgressBarVisibility(false);

            GetThingsStart();
        }

        void GetPersonalImageStart()
        {
            string thingXml = @"        <info>
        <group>
            <filter>
                <type-id>a5294488-f865-4ce3-92fa-187cd3b58930</type-id>
                <thing-state>Active</thing-state>
            </filter>
            <format>
                <section>core</section>
                <section>blobpayload</section>
                <type-version-format>a5294488-f865-4ce3-92fa-187cd3b58930</type-version-format>
                <blob-payload-request>
                    <blob-format>
                        <blob-format-spec>inline</blob-format-spec>
                    </blob-format>
                </blob-payload-request>
            </format>
        </group>
        </info>";

            XElement info = XElement.Parse(thingXml);

            CHBaseRequest request = new CHBaseRequest("GetThings", "3", info, GetPersonalImageCompleted);

            App.HealthVaultService.BeginSendRequest(request);
            SetProgressBarVisibility(true);
        }

        void GetPersonalImageCompleted(object sender, CHBaseResponseEventArgs e)
        {
            SetProgressBarVisibility(false);

            if (e.ErrorText == null)
            {
                _currentThingIds.Clear();

                List<XElement> thingNodes = GetThingsFromResponse(e);
                try
                {
                    if (thingNodes.Count != 0)
                    {
                        string url = thingNodes[0].Descendants("blob-ref-url").Single().Value;

                        HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                        request.BeginGetResponse((ar) =>
                        {
                            var response = request.EndGetResponse(ar);
                            Dispatcher.BeginInvoke(() =>
                            {
                                using (var stream = response.GetResponseStream())
                                {
                                    var image = new BitmapImage();
                                    image.SetSource(stream);
                                    c_RecordImage.Source = image;
                                }
                            });
                        }, null);
                    }
                }
                catch (Exception) { }
            }

            AnimateObjectOpacity(c_RecordPanel);

            GetThingsStart();
        }

        void AnimateObjectOpacity(UIElement element)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                DoubleAnimation animation = new DoubleAnimation();
                CubicEase easing = new CubicEase();
                easing.EasingMode = EasingMode.EaseInOut;
                animation.EasingFunction = easing;
                animation.From = 0.0;
                animation.To = 1.0;

                animation.Duration = new Duration(TimeSpan.FromSeconds(2));

                Storyboard storyboard = new Storyboard();
                storyboard.Children.Add(animation);

                Storyboard.SetTarget(animation, element);
                Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));

                storyboard.Begin();
            });
        }
    }
}