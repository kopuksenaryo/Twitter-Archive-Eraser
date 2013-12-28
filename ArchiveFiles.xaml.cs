using System;
using System.Collections.Generic;
using System.Windows;
using System.Globalization;
using System.Windows.Navigation;
using System.Linq;

using Ionic.Zip;
using System.Diagnostics;



namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for ArchiveFiles.xaml
    /// </summary>
    public partial class ArchiveFiles : Window
    {
        private List<JsFile> jsFiles = new List<JsFile>();
        IEnumerable<YearOfTweets> yearsOfTweets = new List<YearOfTweets>();

        public ArchiveFiles()
        {
            InitializeComponent();
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".zip";
            dlg.Filter = "Zip Archive (*.zip)|*.zip|JS archive files (*.js)|*.js";
            dlg.Multiselect = true;

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                foreach (var item in dlg.FileNames)
	            {
                    //if the file is not already present
                    if (item.EndsWith(".js") && !jsFiles.Any(file => file.Path == item))
                    {
                        jsFiles.Add(new JsFile() { 
                                            Path = item, 
                                            Selected = false, 
                                            OriginZipFile = "", 
                                            Filename = item.Substring(item.LastIndexOf('\\') + 1) 
                        });
                    }

                    if(item.EndsWith(".zip"))
                    {
                        using(ZipFile zipArchive = ZipFile.Read(item))
	                    {
                            foreach (ZipEntry jsFile in zipArchive)
                            {
                                if (jsFile.FileName.EndsWith(".js") && jsFile.FileName.Contains(@"data/js/tweets") && !jsFiles.Any(file => file.Path == jsFile.FileName))
                                {
                                    jsFiles.Add(new JsFile() { 
                                                    Path = jsFile.FileName, 
                                                    Selected = false,
                                                    Filename = jsFile.FileName.Substring(jsFile.FileName.LastIndexOf('/')+1),
                                                    OriginZipFile = item
                                                });
                                }
                            }
	                    }
                    }
	            }


                foreach (var item in jsFiles)
                {
                    int tmpYear = -1;
                    int tmpMonth = -1;

                    if (System.Text.RegularExpressions.Regex.IsMatch(item.Filename, @"\d{4}_\d{2}\.js", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if(!Int32.TryParse(item.Filename.Substring(0, "2013".Length), out tmpYear))
                        {
                            tmpYear = -1;
                        }    

                        if (!Int32.TryParse(item.Filename.Substring(item.Filename.IndexOf('_') + 1, "01".Length), out tmpMonth))
                        {
                            tmpMonth = -1;
                        }

                        if (tmpMonth < 1 || tmpMonth > 12)
                        {
                            tmpMonth = -1;
                        }
                    }
                    
                    item.TweetYear = tmpYear;
                    item.TweetMonth = tmpMonth;

                    if (item.TweetMonth != -1)
                    {
                        item.FriendlyFilename = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(item.TweetMonth);
                    }
                    else
                    {
                        item.FriendlyFilename = item.Filename;
                    }

                    // If not from zip archive
                    if (String.IsNullOrEmpty(item.OriginZipFile))
                    {
                        item.FriendlyFilename += "<external>";
                    }
                }

                yearsOfTweets = jsFiles.GroupBy(jsFile => jsFile.TweetYear,
                                                jsFile => jsFile,
                                                (key, g) => new { Year = key, TweetJsFiles = g })
                                       .Select(a => new YearOfTweets()
                                       {
                                           Year = a.Year,
                                           TweetJsFiles = a.TweetJsFiles.ToList<JsFile>()
                                       });    
            }

            if (jsFiles.Count < 1)
            {
                MessageBox.Show("No Twitter archive *.js files were loaded!", "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            treeFiles.ItemsSource = yearsOfTweets;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            List<JsFile> selectedJsFiles = new List<JsFile>();
            foreach (var itemYear in yearsOfTweets)
            {
                selectedJsFiles.AddRange(itemYear.TweetJsFiles.Where(jsFile => jsFile.Selected == true));
            }

            if (selectedJsFiles.Count == 0)
            {
                MessageBox.Show("Please select at least one *.js file from the twitter archive",
                                "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            Application.Current.Properties["jsFiles"] = selectedJsFiles;

            WebUtils.ReportMonthsToDelete((string)Application.Current.Properties["userName"], 
                                          (string)Application.Current.Properties["sessionGUID"],
                                          selectedJsFiles.Select(jsFile => String.Format("{0}_{1}", jsFile.TweetYear, jsFile.TweetMonth)).ToList());

            DeleteTweets page = new DeleteTweets();
            this.Hide();
            page.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            page.ShowDialog();
            this.Show();
            //Application.Current.Shutdown();
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in jsFiles)
            {
                item.Selected = true;
            }
        }

        private void SelectAllCheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in jsFiles)
            {
                item.Selected = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Window_Closed_1(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

   
}
