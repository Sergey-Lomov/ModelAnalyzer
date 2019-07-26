﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ModelAnalyzer.Parameters.Events
{
    using BranchPiar = ValueTuple<int, int>;

    class BranchPointsAllocation_Standard : BranchPointsAllocation
    {
        public BranchPointsAllocation_Standard()
        {
            type = ParameterType.Inner;
            title = "Распределение очков ветвей (основная вариация)";
            details = "Очки ветвей на картах располагаются согласно набору правил, которые подробно описанны в основном документе по механике";
            fractionalDigits = 0;
        }

        internal override ParameterCalculationReport Calculate(Calculator calculator)
        {
            return Calculate(calculator, true);
        }
    }
}