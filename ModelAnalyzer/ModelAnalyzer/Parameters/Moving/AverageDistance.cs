﻿using System;
using System.Linq;

using ModelAnalyzer.Parameters.Timing;

namespace ModelAnalyzer.Parameters.Moving
{
    class AverageDistance : SingleParameter
    {
        private readonly string arrayIssueFormat = "Длина массивов \"{0}\" и \"{1}\" не совпадает.";

        public AverageDistance()
        {
            type = ParameterType.Inner;
            title = "Среднее расстояние";
            details = "Вычисляется на основе средних расстояний фаз и длительности фаз.";
            fractionalDigits = 2;
        }

        internal override ParameterCalculationReport Calculate(Calculator calculator)
        {

            calculationReport = new ParameterCalculationReport(this);

            float[] pd = calculator.UpdateArrayValue(typeof(AveragePhasesDistance));
            float[] pw = calculator.UpdateArrayValue(typeof(PhasesWeight));

            if (pd.Length != pw.Length)
            {
                string title1 = calculator.ParameterTitle(typeof(AveragePhasesDistance));
                string title2 = calculator.ParameterTitle(typeof(PhasesWeight));
                string message = string.Format(arrayIssueFormat, title1, title2);

                calculationReport.Failed(message);
                return calculationReport;
            }

            float sum = 0;
            for (int i = 0; i < pd.Length; i++)
                sum += pd[i] * pw[i];

            value = unroundValue = sum / pw.Sum();
            return calculationReport;
        }
    }
}
