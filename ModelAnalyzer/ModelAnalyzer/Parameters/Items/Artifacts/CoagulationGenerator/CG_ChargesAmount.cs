﻿using System;

using ModelAnalyzer.Services;

namespace ModelAnalyzer.Parameters.Items.Artifacts.CoagulationGenerator
{
    class CG_ChargesAmount : FloatSingleParameter
    {
        public CG_ChargesAmount()
        {
            type = ParameterType.Out;
            title = "ГС: кол-во зарядов";
            details = "";
            fractionalDigits = 0;
            tags.Add(ParameterTag.items);
            tags.Add(ParameterTag.artifacts);
        }

        internal override ParameterCalculationReport Calculate(Calculator calculator)
        {
            calculationReport = new ParameterCalculationReport(this);

            float eapr = calculator.UpdatedParameter<EstimatedArtifactsProfit>().GetValue();
            float oupr = calculator.UpdatedParameter<CG_OneUsageProfit>().GetValue();

            unroundValue = eapr / oupr;
            value = (float)Math.Round(unroundValue, MidpointRounding.AwayFromZero);

            return calculationReport;
        }
    }
}
