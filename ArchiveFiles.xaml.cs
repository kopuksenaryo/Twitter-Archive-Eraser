using System;
using System.Collections.Generic;
using System.Windows;
using System.Globalization;
using System.Windows.Navigation;
using System.Linq;

using Ionic.Zip;
using System.Diagnostics;
using System.IO;

namespace Twitter_Archive_Eraser
{
    /// <summary>
    /// Interaction logic for ArchiveFiles.xaml
    /// </summary>
    public partial class ArchiveFiles : Window
    {
        List<JsFile> jsFiles = new List<JsFile>();
        List<JsFilesGroup> jsFilesGroupList = new List<JsFilesGroup>();

        public ArchiveFiles()
        {
            InitializeComponent();
            this.Loaded += ArchiveFiles_Loaded;
        }

        void ArchiveFiles_Loaded(object sender, RoutedEventArgs e)
        {
            //this.Title += " v" + ApplicationSettings.GetApplicationSettings().Version;
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".zip";
            dlg.Filter = "Twitter Archive (*.zip) or Tweets .js file (*.js)|*.zip;*.js";
            dlg.Multiselect = true;

            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                foreach (var filename in dlg.FileNames)
	            {
                    //if the file is not already present
                    if (filename.EndsWith(".js") && !jsFiles.Any(file => file.FullPath == filename))
                    {
                        jsFiles.Add(new JsFile() { 
                                            FullPath = filename, 
                                            Selected = false, 
                                            OriginZipFile = "", 
                                            Filename = filename.Substring(filename.LastIndexOf('\\') + 1) 
                        });
                    }

                    if(filename.EndsWith(".zip"))
                    {
                        using(ZipFile zipArchive = ZipFile.Read(filename))
	                    {
                            foreach (ZipEntry jsFile in zipArchive)
                            {
                                if (jsFile.FileName.EndsWith(".js") 
                                    && jsFile.FileName.Contains(@"data/js/tweets") 
                                    && !jsFiles.Any(file => file.FullPath == jsFile.FileName))
                                {
                                    jsFiles.Add(new JsFile()
                                    {
                                        FullPath = jsFile.FileName,
                                        Selected = false,
                                        Filename = Path.GetFileName(jsFile.FileName),
                                        OriginZipFile = filename
                                    });
                                }
                            }
	                    }
                    }
	            }


                foreach (var jsFile in jsFiles)
                {
                    int tmpYear = -1, tmpMonth = -1;

                    // Get the year and month of a given JS file
                    if (System.Text.RegularExpressions.Regex.IsMatch(jsFile.Filename, @"\d{4}_\d{2}\.js", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if(!int.TryParse(jsFile.Filename.Substring(0, "2013".Length), out tmpYear))
                        {
                            tmpYear = -1;
                        }    

                        if (!int.TryParse(jsFile.Filename.Substring(jsFile.Filename.IndexOf('_') + 1, "01".Length), out tmpMonth))
                        {
                            tmpMonth = -1;
                        }

                        if (tmpMonth < 1 || tmpMonth > 12)
                        {
                            tmpMonth = -1;
                        }
                    }
                    
                    jsFile.Year = tmpYear;
                    jsFile.Month = tmpMonth;

                    if (jsFile.Month != -1)
                    {
                        jsFile.FriendlyFilename = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(jsFile.Month);
                    }
                    else
                    {
                        jsFile.FriendlyFilename = jsFile.Filename;
                    }

                    // If not from zip archive
                    if (String.IsNullOrEmpty(jsFile.OriginZipFile))
                    {
                        jsFile.FriendlyFilename += " <external>";
                    }
                }

                var groups = jsFiles.GroupBy(jsFile => jsFile.Year);
                foreach (var group in groups)
                {
                    jsFilesGroupList.Add(new JsFilesGroup() {Key = group.Key, JsFiles = group.ToList() });
                }
            }

            if (jsFiles.Count < 1)
            {
                MessageBox.Show("No Twitter archive or *.js files were loaded!", "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            treeFiles.ItemsSource = jsFilesGroupList;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            List<JsFile> selectedJsFiles = new List<JsFile>();
            foreach (var group in jsFilesGroupList)
            {
                selectedJsFiles.AddRange(group.JsFiles.Where(jsFile => jsFile.Selected == true));
            }

            if (selectedJsFiles.Count == 0)
            {
                MessageBox.Show("Please select at least one month from the twitter archive",
                                "Twitter Archive Eraser", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var settings = ApplicationSettings.GetApplicationSettings();
            settings.JsFiles = selectedJsFiles;

            WebUtils.ReportMonthsToDelete(settings.Username, 
                                          settings.SessionId.ToString(),
                                          selectedJsFiles.Select(jsFile => String.Format("{0}_{1}", jsFile.Year, jsFile.Month)).ToList());

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
