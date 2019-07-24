﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAnalyzer.Parameters.Events
{
    class AverageStabilityIncrement : SingleParameter
    {
        public AverageStabilityIncrement()
        {
            type = ParameterType.Inner;
            title = "Средний прирост стабильности события";
            details = "";
            fractionalDigits = 2;
        }

        internal override ParameterCalculationReport Calculate(Calculator calculator)
        {
            calculationReport = new ParameterCalculationReport(this);

            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float[] sia = calculator.UpdatedArrayValue(typeof(StabilityIncrementAllocation));

            float average = 0;
            for (int i = 0; i < sia.Count(); i++)
                average += i * sia[i] / sia.Sum();

            value = unroundValue = average;

            return calculationReport;
        }
    }
}
