using MillenniumTools.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MillenniumTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.LogInfo("Starting MillenniumTools");
            initViewModel();
            Config.Instance.Change += (s, e) => Dispatcher.BeginInvoke(new Action(initViewModel), DispatcherPriority.Normal);
            InitializeComponent();

            this.Closing += MainWindow_Closing;
        }

        private void initViewModel()
        {
            var vm = DataContext as MainViewmodel;
            disposeViewModel(vm);
            vm = new MainViewmodel();
            vm.GraphItems.ListChanged += GraphItems_ListChanged;
            vm.AlarmSoundRequested += vm_AlarmSoundRequested;
            DataContext = vm;
        }

        void vm_AlarmSoundRequested(object sender, EventArgs e)
        {
            if (Config.Instance.AlarmSoundVolume > 0 && !Config.Instance.IsMuted)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    mediaElement.Play();
                    mediaElement.Position = TimeSpan.Zero;
                }), DispatcherPriority.Normal);
            }
        }

        void GraphLines_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var bezierConverter = (BezierConverter)this.Resources["BezierConverter"];
            bezierConverter.Size = e.NewSize;
        }

        void GraphItems_ListChanged(object sender, ListChangedEventArgs e)
        {
            var canvas = GraphLinesCanvas;

            var width = canvas.ActualWidth;
            var height = canvas.ActualHeight - 20;
            canvas.Children.Clear();
            var items = (BindingList<GraphItem>)sender;
            var dict = new Dictionary<string, PathFigureCollection>();
            var blurEffect = new BlurEffect { Radius = 5 };

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var brush = TextToBrushConverter.GetBrush(item.Ip);
                var line = item as GraphLine;
                if (line != null)
                {
                    var spline = item as GraphSpline;
                    if (spline == null)
                    {
                        canvas.Children.Add(new Line
                        {
                            IsHitTestVisible = false,
                            Stroke = brush,
                            StrokeThickness = 2,
                            StrokeDashArray = line.Dash == null
                                ? null
                                : new DoubleCollection { line.Dash.Value, line.Dash.Value },
                            X1 = line.X * width + item.Offset * 2,
                            Y1 = line.Y * height + 5-item.Offset*2,
                            X2 = line.X2 * width + item.Offset*2,
                            Y2 = line.Y2 * height + 5 - item.Offset * 2,
                            Effect = item.Blur ? blurEffect : null
                        });
                    }
                    else
                    {

                        PathFigureCollection figures;
                        if (!dict.TryGetValue(spline.Ip, out figures))
                        {
                            figures = new PathFigureCollection();
                            var path = new Path
                            {
                                IsHitTestVisible = false,
                                Stroke = brush,
                                StrokeThickness = 2,
                                StrokeDashArray=spline.Dash==null
                                    ? null
                                    : new DoubleCollection { spline.Dash.Value, spline.Dash.Value },
                                Data = new PathGeometry
                                {
                                    Figures = figures
                                },
                                Effect = item.Blur ? blurEffect : null
                            };
                            canvas.Children.Add(path);
                            dict[spline.Ip] = figures;
                        }
                        figures.Add(
                            new PathFigure
                            {
                                StartPoint = new Point
                                {
                                    X = spline.X * width + item.Offset*2,
                                    Y = spline.Y * height + 5 - item.Offset*2
                                },
                                Segments = new PathSegmentCollection{
                                    new BezierSegment
                                    {
                                        IsSmoothJoin = true,
                                        Point1 = new Point
                                        {
                                            X = (spline.X + Config.Instance.Smoothness * (spline.X2 - spline.X)) * width + item.Offset*2,
                                            Y = spline.Y * height+5-item.Offset*2
                                        },
                                        Point2 = new Point
                                        {
                                            X = (spline.X + (1 - Config.Instance.Smoothness) * (spline.X2 - spline.X)) * width + item.Offset*2,
                                            Y = spline.Y2 * height+5-item.Offset*2
                                        },
                                        Point3 = new Point
                                        {
                                            X = spline.X2 * width + item.Offset*2,
                                            Y = spline.Y2 * height+5-item.Offset*2
                                        }
                                    }
                                }
                            }
                        );
                    }
                }
                var start = item as GraphStart;
                if (start != null)
                {
                    var ellipse = new Ellipse
                    {
                        IsHitTestVisible = false,
                        Width = 10,
                        Height = 10,
                        Stroke = brush.Lighter(50),
                        StrokeThickness = 1,
                        Fill = brush,
                        Effect = item.Blur ? blurEffect : null
                    };
                    Canvas.SetLeft(ellipse, start.X * width - 5 + item.Offset*2);
                    Canvas.SetTop(ellipse, start.Y * height - item.Offset*2);
                    canvas.Children.Add(ellipse);
                }
                var end = item as GraphEnd;
                if (end != null)
                {
                    var rect = new Rectangle
                    {
                        IsHitTestVisible = false,
                        Width = 10,
                        Height = 10,
                        Stroke = brush.Lighter(50),
                        StrokeThickness = 1,
                        Fill = brush,
                        Effect = item.Blur ? blurEffect : null
                    };
                    Canvas.SetLeft(rect, end.X * width - 5 + item.Offset*2);
                    Canvas.SetTop(rect, end.Y * height - item.Offset*2);
                    canvas.Children.Add(rect);
                }
                var text = item as GraphText;
                if (text != null)
                {
                    var tb = new TextBlock
                    {
                        IsHitTestVisible = false,
                        Background=Brushes.Transparent,
                        Foreground = brush,
                        Text=text.Text,
                        FontSize=15,
                        TextAlignment = TextAlignment.Center,
                        Effect = item.Blur ? blurEffect : null
                    };
                    Canvas.SetLeft(tb, text.X * width + item.Offset*2);
                    Canvas.SetTop(tb, text.Y * height - item.Offset*2);
                    canvas.Children.Add(tb);
                }
            }
        }


        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.LogInfo("Exiting MillenniumTools");
            var vm = (MainViewmodel)DataContext;
            disposeViewModel(vm);
            Config.Instance.Save();
        }

        private void disposeViewModel(MainViewmodel vm)
        {
            if (vm == null) return;
            vm.GraphItems.ListChanged -= GraphItems_ListChanged;
            vm.AlarmSoundRequested -= vm_AlarmSoundRequested;
            vm.Dispose();
        }

        private void ShowConfig(object sender, RoutedEventArgs e)
        {
            Config.Instance.Save();
            try
            {
                var success = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        Arguments = Config.Instance.ConfigFilePath,
                        FileName = getNotepad(),
                        WindowStyle = ProcessWindowStyle.Maximized
                    }
                }.Start();
                this.LogInfo("Opening configuration: " + success);

            }
            catch (Exception ex)
            {
                this.LogInfo("Error opening configuration: " + ex.Message);
            }
        }

        private string getNotepad()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
        }
    }
}