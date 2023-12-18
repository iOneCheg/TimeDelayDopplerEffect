using ScottPlot.Control;
using ScottPlot;
using System.Text;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

namespace TimeDelayDopplerEffect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SignalGenerate _gS;
        private ModulationType _modulationType;
        private readonly BackgroundWorker _bgResearch;
        private Tuple<ModulationType, int, int, int, int, int, int> signalParam;
        private Dictionary<string, object> modParam;
        public MainWindow()
        {
            InitializeComponent();
            _bgResearch = (BackgroundWorker)FindResource("BackgroundWorkerConductResearch");
        }

        private void OnLoadedMainWindow(object sender, RoutedEventArgs e)
        {
            RbIsAsk.IsChecked = true;

            //Настройка графиков.
            SetUpChart(ChartReferenceSignal, "Искомый сигнал", "Время, с", "Амплитуда");
            SetUpChart(ChartResearchedSignal, "Исследуемый сигнал", "Время, с", "Амплитуда");
            SetUpChart(ChartCorellation, "Взаимная корреляция сигналов", "Время, с", "Амплитуда");
            SetUpChart(ChartResearch, "Зависимость вероятности обнаружения сигнала от ОСШ", "Уровень шума, дБ", "Вероятность обнаружения");
        }
        private static void SetUpChart(IPlotControl chart, string title, string labelX, string labelY)
        {
            chart.Plot.Title(title);
            chart.Plot.XLabel(labelX);
            chart.Plot.YLabel(labelY);
            chart.Plot.XAxis.MajorGrid(enable: true, color: Color.FromArgb(50, Color.Black));
            chart.Plot.YAxis.MajorGrid(enable: true, color: Color.FromArgb(50, Color.Black));
            chart.Plot.XAxis.MinorGrid(enable: true, color: Color.FromArgb(30, Color.Black), lineStyle: LineStyle.Dot);
            chart.Plot.YAxis.MinorGrid(enable: true, color: Color.FromArgb(30, Color.Black), lineStyle: LineStyle.Dot);
            chart.Plot.Margins(x: 0.0, y: 0.8);
            chart.Plot.SetAxisLimits(xMin: 0);
            chart.Configuration.Quality = QualityMode.High;
            chart.Configuration.DpiStretch = false;
            chart.Refresh();
        }

        private void RbIsAsk_Checked(object sender, RoutedEventArgs e)
        {
            _modulationType = ModulationType.ASK;
            GbAskParams.IsEnabled = true;
            GbFskParams.IsEnabled = false;
        }

        private void RbIsFsk_Checked(object sender, RoutedEventArgs e)
        {
            _modulationType = ModulationType.FSK;
            GbAskParams.IsEnabled = false;
            GbFskParams.IsEnabled = true;
        }

        private void RbIsPsk_Checked(object sender, RoutedEventArgs e)
        {
            _modulationType = ModulationType.PSK;
            GbAskParams.IsEnabled = false;
            GbFskParams.IsEnabled = false;
        }

        private void GenerateSignals_Click(object sender, RoutedEventArgs e)
        {
            var countBits = BitsCount.Value;
            var baudRate = BaudRate.Value;
            var carrierFreq = double.Round((double)CarrierFreq.Value, 1) * 1000;
            var samplingFreq = double.Round((double)SamplingFreq.Value, 1) * 1000;
            var delay = Delay.Value;
            var doppler = DopplerFreq.Value;
            var snr = SNR.Value;

            var a0 = A0.Value;
            var a1 = A1.Value;
            var f1 = F1.Value;
            var f0 = F0.Value;

            int MaxIndex = 0;
            //Параметры сигнала для модуляции
            signalParam = new
                (_modulationType, (int)countBits, (int)baudRate, (int)carrierFreq, (int)samplingFreq, (int)delay, (int)doppler);

            //Параметры модуляции
            modParam = new Dictionary<string, object>
            {
                ["a0"] = a0,
                ["a1"] = a1,
                ["f0"] = f0,
                ["f1"] = f1
            };

            ChartReferenceSignal.Visibility = Visibility.Visible;
            ChartResearchedSignal.Visibility = Visibility.Visible;
            ChartCorellation.Visibility = Visibility.Visible;
            ChartResearch.Visibility = Visibility.Collapsed;

            //Рассчет модуляции, затем корреляции и нахождение max
            _gS = new SignalGenerate(signalParam);
            _gS.ModulateSignals(modParam);
            _gS.MakeNoise((double)snr);
            _gS.CalculateCorrelation(out MaxIndex);
            //_gS.CalculateCorrelation(out MaxIndex);

            //var yMax = _gS.desiredSignal.Max(p => double.Abs(p.Y));

            ChartReferenceSignal.Plot.Clear();
            ChartReferenceSignal.Plot.AddSignalXY(_gS.desiredSignal.Select(p => p.X).ToArray(),
                _gS.desiredSignal.Select(p => p.Y).ToArray(), color: Color.Blue);
            ChartReferenceSignal.Refresh();

            ChartResearchedSignal.Plot.Clear();
            ChartResearchedSignal.Plot.AddSignalXY(_gS.researchedSignal.Select(p => p.X).ToArray(),
                _gS.researchedSignal.Select(p => p.Y).ToArray(), color: Color.Blue);
            ChartResearchedSignal.Plot.AddVerticalLine((double)delay / 1000d, Color.Green);
            ChartResearchedSignal.Plot.AddVerticalLine((double)delay / 1000d + _gS.Tsample, Color.Green);
            ChartResearchedSignal.Refresh();

            ChartCorellation.Plot.Clear();
            ChartCorellation.Plot.AddSignalXY(_gS.correlation.Select(p => p.X).ToArray(),
                _gS.correlation.Select(p => p.Y).ToArray(), color: System.Drawing.Color.Blue);
            ChartCorellation.Plot.AddVerticalLine((double)delay / 1000d, Color.Green);
            ChartCorellation.Plot.AddVerticalLine(MaxIndex * _gS.dt, Color.Red);
            ChartCorellation.Refresh();

            //CorrelationDelay.Text = ((MaxIndex * _gS.dt)).ToString();
        }

        private void ConductResearch_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OnDoWorkBackgroundWorkerConductResearch(object sender, System.ComponentModel.DoWorkEventArgs e)
        {

        }

        private void OnRunWorkerCompletedBackgroundWorkerConductResearch(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {

        }

        private void OnProgressChangedBackgroundWorkerConductResearch(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {

        }
    }
}