using DevExpress.XtraPrinting.Native;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TimeDelayDopplerEffect
{
    public enum ModulationType
    {
        ASK,
        FSK,
        PSK
    }
    public struct PointComplex
    {
        public double T;
        public Complex Value;
        public PointComplex(double t, Complex v)
        {
            Value = v; T = t;
        }
    }
    class SignalGenerate
    {
        /// <summary>
        /// Тип модуляции
        /// </summary>
        private ModulationType Type { get; }
        /// <summary>
        /// Количество бит
        /// </summary>
        public int BitsCount { get; }
        /// <summary>
        /// Скорость передачи сигнала
        /// </summary>
        public int BaudRate { get; }
        /// <summary>
        /// Длительность одного бита
        /// </summary>
        public double BitLength => 1d / BaudRate;
        /// <summary>
        /// Длительность опорного сигнала
        /// </summary>
        public double Tsample => BitsCount / (double)BaudRate;
        /// <summary>
        /// Длительность исследуемого сигнала
        /// </summary>
        public double TBigSample => (double)(BitsCountBigPSP * 2 + BitsCount) / BaudRate;
        /// <summary>
        /// Несущая частота
        /// </summary>
        public int CarrierFreq { get; }
        /// <summary>
        /// Доплеровское смещение
        /// </summary>
        public int DopplerFreq { get; }
        /// <summary>
        /// Частота дискретизации
        /// </summary>
        public int SamplingFreq { get; }
        /// <summary>
        /// Шаг между отсчетами
        /// </summary>
        public double dt => 1d / SamplingFreq;
        /// <summary>
        /// Задержка второго сигнала
        /// </summary>
        public int Delay { get; }  // в мс
        /// <summary>
        /// Количество целых бит для исследуемого сигнала
        /// </summary>
        public int BitsCountBigPSP =>
            (Delay % (int)(BitLength * 1000) != 0)
            ? (Delay / (int)(BitLength * 1000) + 1)
            : Delay / (int)(BitLength * 1000);
        /// <summary>
        /// Количество отсчетов на 1 бит
        /// </summary>
        public int TBit => (int)(TBigSample * SamplingFreq / (BitsCountBigPSP * 2 + BitsCount));
        /// <summary>
        /// Номер отсчета для отсечения "лишних" отсчетов не целого бита перед битом опорного сигнала
        /// </summary>
        private int CountStartCut => (int)(Delay / (dt * 1000));
        /// <summary>
        /// Номер отсчета начала опорного сигнала в исследуемом
        /// </summary>
        private int CountStart => BitsCountBigPSP * TBit;
        /// <summary>
        /// Номер отсчета конца опорного сигнала в исследуемом
        /// </summary>
        private int CountEnd => (BitsCountBigPSP + BitsCount) * TBit;
        /// <summary>
        ///  Номер отсчета для отсечения "лишних" отсчетов не целого бита после бита опорного сигнала
        /// </summary>
        private int CountEndCut => CountEnd + CountStartCut;
        /// <summary>
        /// Комплексные отсчеты исследумого сигнала
        /// </summary>
        public List<PointComplex> researchedSignalComplex { get; private set; }
        public List<PointD> researchedSignal { get; private set; }
        public List<PointD> desiredSignal { get; private set; }
        /// <summary>
        /// Комплексные отсчеты опорного сигнала
        /// </summary>
        public List<PointComplex> desiredSignalComplex { get; private set; }
        /// <summary>
        /// Отсчеты корреляции
        /// </summary>
        public List<PointD> correlation { get; private set; }

        public SignalGenerate(Tuple<ModulationType, int, int, int, int, int, int> paramSignal)
        {
            researchedSignalComplex = new List<PointComplex>();
            desiredSignalComplex = new List<PointComplex>();

            researchedSignal = new List<PointD>();
            desiredSignal = new List<PointD>();

            Type = paramSignal.Item1;
            BitsCount = paramSignal.Item2;
            BaudRate = paramSignal.Item3;
            CarrierFreq = paramSignal.Item4;
            SamplingFreq = paramSignal.Item5;
            Delay = paramSignal.Item6;
            DopplerFreq = paramSignal.Item7;
        }
        /// <summary>
        /// Генерация ПСП для исследуемого сигнала
        /// </summary>
        /// <param name="rnd"></param>
        /// <param name="CountBitsInDelay"></param>
        /// <param name="BitsCount"></param>
        /// <returns></returns>
        public static int[] BigPSPGenerate(Random rnd, int CountBitsInDelay, int BitsCount) =>
            Enumerable
            .Range(0, CountBitsInDelay * 2 + BitsCount)
            .Select(i => Convert.ToInt32(rnd.Next(2) == 0))
            .ToArray();

        public void ModulateSignals(Dictionary<string, object> modulateParam)
        {
            //Рассчет ПСП
            int[] Bigpsp = BigPSPGenerate(new Random(Guid.NewGuid().GetHashCode()),
              BitsCountBigPSP, BitsCount);

            int counter = 0, counterAll = 0;

            //Внешний цикл по длине ПСП
            for (int bit = 0; bit < Bigpsp.Length; bit++)
                //Цикл по каждому биту
                for (int i = 0; i < TBit; i++)
                {
                    //Условие для "отрезания" отсчетов не целых бит при не кратной задержке
                    if (!((counterAll > CountStartCut && counterAll < CountStart) || (counterAll > CountEndCut)))
                    {
                        double kAmplitude = (Bigpsp[bit] == 1d) ? (double)modulateParam["a1"] : (double)modulateParam["a0"],
                               kPhase = (Bigpsp[bit] == 1d) ? Math.PI : 0,
                               kFreq = (Bigpsp[bit] == 1d) ? (int)modulateParam["f1"] : (int)modulateParam["f0"];

                        Complex v = Type switch
                        {
                            ModulationType.ASK =>
                            Complex.FromPolarCoordinates(
                                kAmplitude, 2 * Math.PI * (CarrierFreq + DopplerFreq) * counter * dt),
                            //kAmplitude * Complex.Exp(2 * Math.PI * (CarrierFreq + DopplerFreq) * counter * dt),

                            ModulationType.PSK =>
                            Complex.FromPolarCoordinates(1, 2 * Math.PI * (CarrierFreq + DopplerFreq) * counter * dt + Math.PI + kPhase),
                            
                            ModulationType.FSK =>
                            Complex.FromPolarCoordinates(1, 2 * Math.PI * (kFreq + DopplerFreq) * counter * dt),

                            _ => 0
                        };
                        researchedSignalComplex.Add(new PointComplex(counter * dt, v));

                        //Условие для отсчетов опорного сигнала
                        if (counter >= CountStart && counter < CountEnd)
                        {
                            Complex t = Type switch
                            {
                                ModulationType.ASK =>
                                Complex.FromPolarCoordinates(
                                kAmplitude, 2 * Math.PI * CarrierFreq * counter * dt),

                                ModulationType.PSK =>
                                Complex.FromPolarCoordinates(1, 2 * Math.PI * CarrierFreq * counter * dt + Math.PI + kPhase),

                                ModulationType.FSK =>
                                Complex.FromPolarCoordinates(1, 2 * Math.PI * kFreq * counter * dt),

                                _ => 0
                            };
                            desiredSignalComplex.Add(new PointComplex(counter * dt, t));
                        }

                        counter++;
                    }
                    counterAll++;
                }

        }
        /// <summary>
        /// Наложение шума на сигналы
        /// </summary>
        /// <param name="snrDb">SNR в дБ</param>
        public void MakeNoise(double snrDb)
        {
            // Наложение шума на искомый сигнал.
            desiredSignalComplex = desiredSignalComplex.Zip(
                GenerateNoise(desiredSignalComplex.Count,
                desiredSignalComplex.Sum(p => p.Value.Real * p.Value.Real),
                desiredSignalComplex.Sum(p => p.Value.Imaginary * p.Value.Imaginary), 10),
                (p, n) => new PointComplex(p.T, p.Value + n))
                .ToList();

            desiredSignal = desiredSignalComplex
                .Select(p => new PointD(p.T, p.Value.Magnitude))
                .ToList();
            // Наложение шума на исследуемый сигнал.
            researchedSignalComplex = researchedSignalComplex.Zip(
                GenerateNoise(researchedSignalComplex.Count,
                researchedSignalComplex.Sum(p => p.Value.Real * p.Value.Real),
                researchedSignalComplex.Sum(p => p.Value.Imaginary * p.Value.Imaginary), snrDb),
                (p, n) => new PointComplex(p.T, p.Value + n))
                .ToList();

            researchedSignal = researchedSignalComplex
                .Select(p => new PointD(p.T, p.Value.Magnitude))
                .ToList();
        }
        /// <summary>
        /// Рассчет корреляции
        /// </summary>
        /// <param name="maxIndex">Индекс max корреляции</param>
        public void CalculateCorrelation(out int maxIndex)
        {
            var result = new List<PointD>();
            var maxCorr = double.MinValue;
            var index = 0;
            for (var i = 0; i < researchedSignalComplex.Count - desiredSignalComplex.Count + 1; i++)
            {
                Complex corr = Complex.Zero;
                for (var j = 0; j < desiredSignalComplex.Count; j++)
                    corr += researchedSignalComplex[i + j].Value * Complex.Conjugate(desiredSignalComplex[j].Value);
                result.Add(new PointD(researchedSignalComplex[i].T, Complex.Abs(corr) / desiredSignalComplex.Count));

                if (result[i].Y > maxCorr)
                {
                    maxCorr = result[i].Y;
                    index = i;
                }
            }

            maxIndex = index;
            correlation = result;
        }
        /// <summary>
        /// Генерация случайного числа по нормальному распределению
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static double GetNormalRandom(double min, double max, int n = 12)
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            var sum = 0d;
            for (var i = 0; i < n; i++)
                sum += rnd.NextDouble() * (max - min) + min;
            return sum / n;
        }
        /// <summary>
        /// Генерация белого гауссовского шума
        /// </summary>
        /// <param name="countNumbers">Число отсчетов</param>
        /// <param name="energySignal">Энергия сигнала</param>
        /// <param name="snrDb">SNR в дБ</param>
        /// <returns></returns>
        private static IEnumerable<Complex> GenerateNoise(int countNumbers, double energySignalReal,
            double energySignalImaginary, double snrDb)
        {
            var noise = new List<double>();
            var noiseResult = new List<Complex>();
            for (var i = 0; i < countNumbers; i++)
            {
                noise.Add(GetNormalRandom(-1d, 1d));
                noiseResult.Add(new Complex(GetNormalRandom(-1d, 1d), GetNormalRandom(-1d, 1d)));
            }
                noise.Add(GetNormalRandom(-1d, 1d));

            // Нормировка шума.
            var snr = Math.Pow(10, -snrDb / 10); 
            var normReal = Math.Sqrt(snr * energySignalReal / noise.Sum(y => y * y));
            var normImag = Math.Sqrt(snr * energySignalImaginary / noise.Sum(y => y * y));

            return noiseResult
                .Select(y => new Complex(y.Real * normReal, y.Imaginary * normImag))
                .ToList();
        } 
    }
}
