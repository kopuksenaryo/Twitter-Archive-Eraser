using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

using LinqToTwitter;
using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Linq;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string twitterConsumerKey = ConfigurationManager.AppSettings["twitterConsumerKey"];
        private string twitterConsumerSecret = ConfigurationManager.AppSettings["twitterConsumerSecret"];

        private string twitterConsumerKeyDM = ConfigurationManager.AppSettings["twitterConsumerKeyDM"];
        private string twitterConsumerSecretDM = ConfigurationManager.AppSettings["twitterConsumerSecretDM"];

        bool needsDMPermissions = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title += " v" + ApplicationSettings.GetApplicationSettings().Version;
        }

        IAuthorizer PerformAuthorization()
        {
            // validate that credentials are present
            if (string.IsNullOrWhiteSpace(twitterConsumerKey) 
                || string.IsNullOrWhiteSpace(twitterConsumerSecret)
                || string.IsNullOrWhiteSpace(twitterConsumerKeyDM) 
                || string.IsNullOrWhiteSpace(twitterConsumerSecretDM))
            {
                MessageBox.Show(@"Error while setting " +
                                    "App.config/appSettings. \n\n" +
                                    "You need to provide your twitterConsumerKey and twitterConsumerSecret in App.config \n" +
                                    "Please visit http://dev.twitter.com/apps for more info.\n");

                return null;
            }

            InMemoryCredentialStore credentialStore = null;
            if(needsDMPermissions)
            {
                credentialStore = new InMemoryCredentialStore()
                {
                    ConsumerKey = twitterConsumerKeyDM,
                    ConsumerSecret = twitterConsumerSecretDM
                };
            }
            else
            {
                credentialStore = new InMemoryCredentialStore()
                {
                    ConsumerKey = twitterConsumerKey,
                    ConsumerSecret = twitterConsumerSecret
                };
            }

            // configure the OAuth object
            var auth = new PinAuthorizer
            {
                CredentialStore = credentialStore,
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

            var settings = ApplicationSettings.GetApplicationSettings();
            settings.Context = new TwitterContext(auth);
            settings.Username = auth.CredentialStore.ScreenName;
            settings.UserID = auth.CredentialStore.UserID;
            settings.SessionId = Guid.NewGuid();
            
            userName.Text = "@" + settings.Username;
            btnAuthorize.IsEnabled = false;

            WebUtils.ReportNewUser(settings.Username, settings.SessionId.ToString());

            stackWelcome.Opacity = 0.0;
            stackWelcome.Visibility = System.Windows.Visibility.Visible;
            FadeAnimation(stackAuthorize, 1.0, 0.0, 200);
            FadeAnimation(stackWelcome, 0.0, 1.0, 2000);
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var settings = ApplicationSettings.GetApplicationSettings();
            settings.EraseType = ApplicationSettings.EraseTypes.TweetsAndRetweets;

            ArchiveFiles page = new ArchiveFiles();
            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            page.ShowDialog();
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

        void FadeAnimation(FrameworkElement control, double animationOpacityFrom, double animationOpacityTo, int animationDurationInMilliSec)
        {
            // Create a storyboard to contain the animations.
            Storyboard storyboard = new Storyboard();
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, animationDurationInMilliSec);

            DoubleAnimation animation = new DoubleAnimation();
            animation.From = animationOpacityFrom;
            animation.To = animationOpacityTo;
            animation.Duration = new Duration(duration);

            // Configure the animation to target de property Opacity
            Storyboard.SetTargetName(animation, control.Name);
            Storyboard.SetTargetProperty(animation, new PropertyPath(System.Windows.Controls.Control.OpacityProperty));
            
            // Add the animation to the storyboard
            storyboard.Children.Add(animation);
            storyboard.Begin(this);
        }

        private void btnRemoveFavorites_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var settings = ApplicationSettings.GetApplicationSettings();
            settings.EraseType = ApplicationSettings.EraseTypes.Favorites;

            FetchTweets page = new FetchTweets();
            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            page.ShowDialog();
        }

        private void chkDeleteDm_Click(object sender, RoutedEventArgs e)
        {
            needsDMPermissions = chkDeleteDm.IsChecked == true ? true : false;
            btnRemoveDM.IsEnabled = needsDMPermissions;
        }

        private void btnRemoveDM_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var settings = ApplicationSettings.GetApplicationSettings();
            settings.EraseType = ApplicationSettings.EraseTypes.DirectMessages;

            FetchTweets page = new FetchTweets();
            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            page.ShowDialog();
        }
    }
}
