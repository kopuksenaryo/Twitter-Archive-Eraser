using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using LinqToTwitter;
using System.Windows.Media.Animation;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for SendTweet.xaml
    /// </summary>
    public partial class SendTweet : Window
    {
        public SendTweet()
        {
            InitializeComponent();

            this.Loaded += SendTweet_Loaded;
        }

        void SendTweet_Loaded(object sender, RoutedEventArgs e)
        {
            txtTweetText.Text = String.Format("Just deleted {0} tweets using Twitter Archive Eraser by @martani_net. Check it out here http://martani.github.io/Twitter-Archive-Eraser/",
                                              Application.Current.Properties["nbTeetsDeleted"]);
        }

        private void btnSendTweet_Click(object sender, RoutedEventArgs e)
        {
            btnSendTweet.IsEnabled = false;
            TwitterContext ctx = (TwitterContext)Application.Current.Properties["context"];

            Status tweet = null;

            try
            {
                tweet = ctx.TweetAsync(txtTweetText.Text).Result;
            }
            catch (Exception)
            {
            };
            
            if (tweet != null)
            {
                txtTweetUpdateStatus.Text = "Sent ...";
                DialogResult = true;
                this.Close();
            }
            else
            {
                txtTweetUpdateStatus.Text = "Failed to send tweet, please try again";
                btnSendTweet.IsEnabled = true;
            }
        }

        private bool closeCompleted = false;


        private void FormFadeOut_Completed(object sender, EventArgs e)
        {
            closeCompleted = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!closeCompleted)
            {
                FormFadeOut.Begin();
                e.Cancel = true;
            }
        }
    }
}
