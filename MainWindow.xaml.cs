using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

using LinqToTwitter;
using System;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string twitterConsumerKey = ConfigurationManager.AppSettings["twitterConsumerKey"];
        private string twitterConsumerSecret = ConfigurationManager.AppSettings["twitterConsumerSecret"];

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        ITwitterAuthorizer PerformAuthorization()
        {
            // validate that credentials are present
            if (string.IsNullOrWhiteSpace(twitterConsumerKey) ||
                string.IsNullOrWhiteSpace(twitterConsumerSecret))
            {
                MessageBox.Show(@"Error while setting " +
                                    "App.config/appSettings. \n\n" +
                                    "You need to provide your twitterConsumerKey and twitterConsumerSecret in App.config \n" +
                                    "Please visit http://dev.twitter.com/apps for more info.\n");

                return null;
            }

            // configure the OAuth object
            var auth = new PinAuthorizer
            {
                Credentials = new InMemoryCredentials
                {
                    ConsumerKey = twitterConsumerKey,
                    ConsumerSecret = twitterConsumerSecret
                },
                UseCompression = true,
                GoToTwitterAuthorization = pageLink => Process.Start(pageLink),
                GetPin = () =>
                {
                    // this executes after user authorizes, which begins with the call to auth.Authorize() below.
                    
                    PinWindow pinw = new PinWindow();
                    pinw.Owner = this;
                    if (pinw.ShowDialog() == true)
                        return pinw.Pin;
                    else
                        return "";
                }
            };

            // start the authorization process (launches Twitter authorization page).
            try
            {
                auth.Authorize();
            }
            catch (WebException ex)
            {
                /*MessageBox.Show("Unable to authroize with Twitter right now. Please check pin number", "Twitter Archive Eraser",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                */
                MessageBox.Show(ex.Message);

                return null;
            }

            return auth;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void btnAuthorize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            chkAcceptToShare.IsEnabled = false;

            ITwitterAuthorizer auth = PerformAuthorization();

            if (auth == null)
                return;

            var ctx = new TwitterContext(auth);

            Application.Current.Properties["context"] = ctx;
            Application.Current.Properties["userName"] = ctx.UserName;
            Application.Current.Properties["sessionGUID"] = Guid.NewGuid().ToString();

            userName.Text = "@" + ctx.UserName;
            stackWelcome.Visibility = System.Windows.Visibility.Visible;
            btnAuthorize.IsEnabled = false;

            WebUtils.ReportNewUser(ctx.UserName, (string)Application.Current.Properties["sessionGUID"]);
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            ArchiveFiles page = new ArchiveFiles();
            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            page.ShowDialog();
            //Application.Current.Shutdown();
        }

        private void Hyperlink_RequestNavigate_1(object sender, RequestNavigateEventArgs e)
        {
            Information info = new Information();
            info.ShowDialog();
        }

        private void AcceptToShare_Click(object sender, RoutedEventArgs e)
        {
            if (chkAcceptToShare.IsChecked != null && chkAcceptToShare.IsChecked == true)
            {
                btnAuthorize.IsEnabled = true;
            }
            else
            {
                btnAuthorize.IsEnabled = false;
            }
        }
    }
}
