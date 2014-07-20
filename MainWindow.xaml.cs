using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

using LinqToTwitter;
using System;
using System.Configuration;
using System.Threading.Tasks;

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

        IAuthorizer PerformAuthorization()
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
                CredentialStore = new InMemoryCredentialStore
                {
                    ConsumerKey = twitterConsumerKey,
                    ConsumerSecret = twitterConsumerSecret
                },
                GoToTwitterAuthorization = pageLink => Process.Start(pageLink),
                GetPin = () =>
                {
                    // ugly hack
                    string pin = "";

                    Dispatcher.Invoke((Action)
                    (() =>
                        {
                            PinWindow pinw = new PinWindow();
                            pinw.Owner = this;
                            if (pinw.ShowDialog() == true)
                                pin = pinw.Pin;
                            else
                                pin = "";
                        }
                    ));

                    return pin;
                }
            };

            return auth;

            // start the authorization process (launches Twitter authorization page).
            try
            {
                Task t = auth.BeginAuthorizeAsync();
                t.Wait();
            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.Message
                                + "\n\nPlease make sure that:"
                                + "\n\t- your computer's date/time is accurate;"
                                + "\n\t- you entered the exact PIN returned by Twitter.",
                                "Twitter Archive Eraser");

                return null;
            }

            return auth;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private async void btnAuthorize_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            chkAcceptToShare.IsEnabled = false;

            var auth = PerformAuthorization();

            try
            {
                await auth.AuthorizeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("\n\nPlease make sure that:"
                                + "\n\t- your computer's date/time is accurate;"
                                + "\n\t- you entered the exact PIN returned by Twitter."
                                + "\n\n\nTwitter error message: " + ex.Message,
                                "Twitter Archive Eraser");

                return;
            }
            

            if (auth == null)
                return;

            var ctx = new TwitterContext(auth);
            var screenName = auth.CredentialStore.ScreenName;

            Application.Current.Properties["context"] = ctx;
            Application.Current.Properties["userName"] = screenName;
            Application.Current.Properties["sessionGUID"] = Guid.NewGuid().ToString();

            userName.Text = "@" + screenName;
            stackWelcome.Visibility = System.Windows.Visibility.Visible;
            btnAuthorize.IsEnabled = false;

            WebUtils.ReportNewUser(screenName, (string)Application.Current.Properties["sessionGUID"]);
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
