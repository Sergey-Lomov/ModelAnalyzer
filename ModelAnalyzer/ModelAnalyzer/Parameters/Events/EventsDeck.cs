﻿using System;
using System.Collections.Generic;
using System.Linq;

using ModelAnalyzer.DataModels;
using ModelAnalyzer.Parameters.Activities;
using ModelAnalyzer.Parameters.Mining;
using ModelAnalyzer.Parameters.Timing;
using ModelAnalyzer.Parameters.Events.Weight;

namespace ModelAnalyzer.Parameters.Events
{
    class EventsDeck : Parameter
    {
        internal List<EventCard> deck = new List<EventCard>();
        const string valueStub = "Колода";
        private readonly string roundingIssue = "Невозможно корректно округлить значения при распределении. Сумма округленных значений отличется суммы не округленных.";
        private readonly string zeroTemplatesIssues = "Невозможно найти шаблон очков ветвей с ненулевым кол-вом карт";

        readonly int[] backPosPriority = new int[3] { 1, 0, 2 };
        readonly int[] frontPosPriority = new int[3] { 4, 3, 5 };

        public EventsDeck()
        {
            type = ParameterType.Out;
            title = "Колода карт событий";
            details = "";
            fractionalDigits = 0;
        }

        internal override ParameterCalculationReport Calculate(Calculator calculator)
        {
            calculationReport = new ParameterCalculationReport(this);
            deck.Clear();

            float na = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float pa = calculator.UpdatedSingleValue(typeof(MaxPlayersAmount));

            float ar = calculator.UpdatedSingleValue(typeof(ArtifactsRarity));
            float asi = calculator.UpdatedSingleValue(typeof(AverageStabilityIncrement));
            float mbr = calculator.UpdatedSingleValue(typeof(MinBackRelations));

            float max_mb = calculator.UpdatedSingleValue(typeof(EventMaxMiningBonus));
            float min_mb = calculator.UpdatedSingleValue(typeof(EventMinMiningBonus));
            float aap = calculator.UpdatedSingleValue(typeof(ArtifactsAvaliabilityPhase));
            float mbc = calculator.UpdatedSingleValue(typeof(EventMiningBonusConstraint));

            float[] mba = calculator.UpdatedArrayValue(typeof(EventMiningBonusAllocation));
            float[] raa_ob = calculator.UpdatedArrayValue(typeof(RelationsAmountAllocation_OB));

            var cards_2d = BothDirectionCards(calculator);
            int amount_ob = (int)na - cards_2d.Count();
            var cards_ob = OnlyBackCards(calculator, amount_ob);

            deck.AddRange(cards_2d);
            deck.AddRange(cards_ob);

            PairReasons(deck, calculator);
            UpdateUsability(deck, calculator);
            UpdateWeight(deck, calculator);
            AddArtifacts(deck, calculator);
            UpdateWeight(deck, calculator);
            AddStabilityIncrement(deck, calculator);
            UpdateWeight(deck, calculator);
            AddMiningBonuses(deck, calculator);
            UpdateWeight(deck, calculator);
            AddBranchPoints(deck, calculator);

            return calculationReport;
        }

        public override void SetupByString(string str)
        {
            // Not possible. This parameter should be calculated.
        }

        public override string StringRepresentation()
        {
            return valueStub;
        }

        public override string UnroundValueToString()
        {
            return valueStub;
        }

        public override string ValueToString()
        {
            return valueStub;
        }

        private void ArrangeRelations(List<EventRelation> relations)
        {
            int backCounter = 0;
            int frontCounter = 0;
            foreach (EventRelation relation in relations)
            {
                bool isFront = relation.direction == RelationDirection.front;
                int position = isFront ? frontPosPriority[frontCounter] : backPosPriority[backCounter];
                relation.position = position;

                backCounter += isFront ? 0 : 1;
                frontCounter += isFront ? 1 : 0;

                backCounter = backCounter == backPosPriority.Count() ? 0 : backCounter;
                frontCounter = frontCounter == frontPosPriority.Count() ? 0 : frontCounter;
            }
        }

        private List<EventCard> BothDirectionCards (Calculator calculator)
        {
            float na = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float mbr = calculator.UpdatedSingleValue(typeof(MinBackRelations));
            float frc = calculator.UpdatedSingleValue(typeof(FrontRelationsCoef));
            float bec_2d = calculator.UpdatedSingleValue(typeof(BlockEventsCoef_2D));
            float[] raa_2d = calculator.UpdatedArrayValue(typeof(RelationsAmountAllocation_2D));

            var cards = new List<EventCard>();

            int cardAmount = (int)Math.Round(na * frc);
            int blockerAmount = (int)Math.Round(cardAmount * bec_2d);
            int blockerCounter = 0;

            for (int i = 0; i < raa_2d.Count(); i++)
            {
                // Generate cards with current relations amount
                var relationsAmount = (int)(mbr + i + 1);
                var raCardAmountF = raa_2d[i] / raa_2d.Sum() * cardAmount;
                var raBlockerAmountF = raa_2d[i] / raa_2d.Sum() * blockerAmount;

                var raCardAmount = (int)Math.Round(raCardAmountF);
                var raBlockerAmount = (int)Math.Round(raBlockerAmountF);
                if (i == raa_2d.Count() - 1)
                {
                    raCardAmount = cardAmount - cards.Count;
                    raBlockerAmount = blockerAmount - blockerCounter;
                }
                blockerCounter += raBlockerAmount;

                var raCards = new List<EventCard>();
                var directionsAllocations = new List<(int backAmount, int frontAmount)>();

                for (int j = 0; j < relationsAmount; j++)
                {
                    if (j >= mbr)
                        directionsAllocations.Add((j, relationsAmount - j));
                }

                // Generate blockers bit-mask
                int currentBlockMask = 0;
                int blockMaskStep = (int)Math.Round((1 << relationsAmount) / (float)raCardAmount);
                blockMaskStep = blockMaskStep == 0 ? 1 : blockMaskStep;

                var raBlockersCounter = 0;
                for (int j = 0; j < directionsAllocations.Count; j++)
                {
                    // Generate cards with current directions allocation
                    var (backAmount, frontAmount) = directionsAllocations[j];
                    var daCardAmount = (int)Math.Round(raCardAmount / (float)directionsAllocations.Count);
                    var daBlockerAmount = (int)Math.Round(raBlockerAmount / (float)directionsAllocations.Count);
                    if (j == directionsAllocations.Count - 1)
                    {
                        daCardAmount = raCardAmount - raCards.Count;
                        daBlockerAmount = raBlockerAmount - raBlockersCounter;
                    }
                    raBlockersCounter += daBlockerAmount;

                    int daBlockerCounter = 0;
                    for (int cardIter = 0; cardIter < daCardAmount; cardIter++)
                    {
                        bool hasFrontBlocker = false;
                        var relations = new List<EventRelation>();
                        for (int relIter = 0; relIter < relationsAmount; relIter++)
                        {
                            RelationDirection direction = relIter < backAmount ? RelationDirection.back : RelationDirection.front;
                            bool isBlocker = (currentBlockMask & (1 << relIter)) != 0;
                            if (direction == RelationDirection.front)
                                if (daBlockerCounter == daBlockerAmount)
                                    isBlocker = false;
                                else if (!hasFrontBlocker)
                                    isBlocker = true;

                            if (isBlocker && direction == RelationDirection.front)
                                hasFrontBlocker = true;

                            RelationType type = isBlocker ? RelationType.blocker : RelationType.reason;
                            EventRelation relation = new EventRelation(type, direction, 0);
                            relations.Add(relation);
                        }

                        ArrangeRelations(relations);
                        var card = new EventCard();
                        card.relations = relations;
                        raCards.Add(card);

                        daBlockerCounter += hasFrontBlocker ? 1 : 0;
                        currentBlockMask += blockMaskStep;
                    }
                }
                cards.AddRange(raCards);
            }

            return cards;
        }

        private List<EventCard> OnlyBackCards(Calculator calculator, int cardAmount)
        {
            float mbr = calculator.UpdatedSingleValue(typeof(MinBackRelations));
            float brc_ob = calculator.UpdatedSingleValue(typeof(BlockRelationsCoef_OB));
            float[] raa_ob = calculator.UpdatedArrayValue(typeof(RelationsAmountAllocation_OB));
            float[] mbca_ob = calculator.UpdatedArrayValue(typeof(MultyblockCardsAllocation_OB));

            var cards = new List<EventCard>();

            for (int raIter = 0; raIter < raa_ob.Count(); raIter++)
            {
                // Generate cards with current relations amount
                var relationsAmount = raIter + (int)mbr;
                var raCardAmountF = raa_ob[raIter] / raa_ob.Sum() * cardAmount;
                var raBlockerAmountF = raCardAmountF * relationsAmount * brc_ob;

                var raCardAmount = (int)Math.Round(raCardAmountF);
                var raBlockerAmount = (int)Math.Round(raBlockerAmountF);
                if (raIter == raa_ob.Count() - 1)
                    raCardAmount = cardAmount - cards.Count;

                var raCards = new List<EventCard>();

                var multyblockRelAllocation = new float[relationsAmount];
                for (int blockersIter = 0; blockersIter < relationsAmount && blockersIter < mbca_ob.Count(); blockersIter++)
                    multyblockRelAllocation[blockersIter] = (blockersIter + 1) * mbca_ob[blockersIter];

                for (int blockersPerCard = 1; blockersPerCard <= relationsAmount; blockersPerCard++)
                {
                    var allCardsBlockers = multyblockRelAllocation[blockersPerCard - 1] / multyblockRelAllocation.Sum() * raBlockerAmount;
                    var blockersCradsAmoutn = (int)Math.Round(allCardsBlockers / blockersPerCard);
                    var blcokersCard = BackOnlyCards(blockersCradsAmoutn, relationsAmount, blockersPerCard);
                    raCards.AddRange(blcokersCard);
                }

                var noBlockersCardAmount = raCardAmount - raCards.Count;
                var noBlcokersCard = BackOnlyCards(noBlockersCardAmount, relationsAmount, 0);
                raCards.AddRange(noBlcokersCard);

                cards.AddRange(raCards);
            }

            return cards;
        }

        private List<EventCard> BackOnlyCards(int cardsAmount, int relationsAmount, int blockersPerCardAmount)
        {
            var cards = new List<EventCard>();
            int currentBlockMask = 0;
            for (int cardIter = 0; cardIter < cardsAmount; cardIter++) {

                while (BlockersAtMask(currentBlockMask, relationsAmount) != blockersPerCardAmount)
                    currentBlockMask++;

                var relations = new List<EventRelation>();
                for (int relIter = 0; relIter < relationsAmount; relIter++)
                {
                    bool isBlocker = (currentBlockMask & (1 << relIter)) != 0;
                    RelationType type = isBlocker ? RelationType.blocker : RelationType.reason;
                    EventRelation relation = new EventRelation(type, RelationDirection.back, 0);
                    relations.Add(relation);
                }

                ArrangeRelations(relations);
                var card = new EventCard();
                card.relations = relations;
                cards.Add(card);

                currentBlockMask++;
            }

            return cards;
        }

        private int BlockersAtMask(int mask, int relationsAmount)
        {
            int blockersAtMask = 0;
            for (int relIter = 0; relIter < relationsAmount; relIter++)
                if ((mask & (1 << relIter)) != 0)
                    blockersAtMask++;

            return blockersAtMask;
        }

        private void PairReasons(List<EventCard> deck, Calculator calculator)
        {
            float p2c = calculator.UpdatedSingleValue(typeof(Pairing2Coef));
            float p3c = calculator.UpdatedSingleValue(typeof(Pairing3Coef));

            var p2available = new List<EventCard>();
            var p3available = new List<EventCard>();

            foreach (EventCard card in deck)
            {
                var availableReasons = card.relations.Where(r => r.type == RelationType.reason && r.direction == RelationDirection.back);
                if (availableReasons.Count() == 2)
                    p2available.Add(card);
                else if (availableReasons.Count() == 3)
                    p3available.Add(card);
            }

            PairReasonsWithCoef(p2available, p2c);
            PairReasonsWithCoef(p3available, p3c);
        }

        private void PairReasonsWithCoef(List<EventCard> cards, float pairingCoefficient)
        {
            float accamulator = 0;
            float step = 1 / pairingCoefficient;
            int iter = 0;
            int counter = 0;
            while (counter < cards.Count * pairingCoefficient)
            {
                if (accamulator >= step)
                {
                    accamulator -= step;
                    counter++;
                    var card = cards[iter];
                    PairReasons(card);
                }

                iter++;
                iter = iter < cards.Count ? iter : 0;
                accamulator++;
            }
        }

        private void PairReasons(EventCard card)
        {
            foreach (EventRelation relation in card.relations)
                if (relation.direction == RelationDirection.back && relation.type == RelationType.reason)
                    relation.type = RelationType.paired_reason;
        }

        private void UpdateUsability(List<EventCard> cards, Calculator calculator)
        {
            float[] abr = calculator.UpdatedArrayValue(typeof(NodesAvailableBackRelations));
            foreach (EventCard card in cards)
                card.usability = UsabilityForCard(card, abr);
        }

        private float UsabilityForCard(EventCard card, float[] availableBackAllocation)
        {
            int backAmount = card.relations.Where(r => r.direction == RelationDirection.back).Count();
            int frontAmount = card.relations.Where(r => r.direction == RelationDirection.front).Count();

            int backRange = 0;
            int frontRange = 0;
            var back = card.relations.Where(r => r.direction == RelationDirection.back).OrderBy(r => r.position);
            var front = card.relations.Where(r => r.direction == RelationDirection.front).OrderBy(r => r.position);

            if (back.Count() > 0)
                backRange = back.Last().position - back.First().position + 1;

            if (front.Count() > 0)
                frontRange = front.Last().position - front.First().position + 1;

            int combintaions = 0;
            for (int availableBack = 0; availableBack < availableBackAllocation.Count(); availableBack++)
            {
                int availableFront = EventRelation.MaxRelationPosition - availableBack;
                if (backAmount > availableBack || frontAmount > availableFront)
                    continue;

            /*    int backRotatinos = availableBack - backAmount + 1;
                int frontRotatinos = availableFront - frontAmount + 1;
                int rotations = backRotatinos < frontRotatinos ? backRotatinos : frontRotatinos;

                combintaions += (int)availableBackAllocation[availableBack] * rotations ;*/
                

                List<int> constraints = new List<int>();

                if (backRange > 0)
                    constraints.Add(availableBack - backRange + 1);
                if (frontRange > 0)
                    constraints.Add(availableFront - frontRange + 1);
                if (backRange > 0 && frontRange > 0)
                {
                    var clocwiseConstraint = front.First().position - back.Last().position;
                    var unclocwiseConstraint = (EventRelation.MaxRelationPosition - front.Last().position) + back.First().position;
                    constraints.Add(clocwiseConstraint);
                    constraints.Add(unclocwiseConstraint);
                }

                var rotations = constraints.Min();
                combintaions += (int)availableBackAllocation[availableBack] * rotations;
            }

            var totalNodesAmount = availableBackAllocation.Sum();
            return combintaions / totalNodesAmount;
        }

        private void UpdateWeight(List<EventCard> cards, Calculator calculator)
        {
            float brw = calculator.UpdatedSingleValue(typeof(BaseRelationsWeight));
            float arw = calculator.UpdatedSingleValue(typeof(AdditionalReasonsWeight));
            float frw = calculator.UpdatedSingleValue(typeof(FrontReasonsWeight));
            float fbw = calculator.UpdatedSingleValue(typeof(FrontBlockerWeight));
            float aw = calculator.UpdatedSingleValue(typeof(ArtifactsWeight));
            float ars = calculator.UpdatedSingleValue(typeof(AverageRelationStability));
            float eip = calculator.UpdatedSingleValue(typeof(EventImpactPrice));
            float am = calculator.UpdatedSingleValue(typeof(AverageMining));

            int maxpa = (int)calculator.UpdatedSingleValue(typeof(MaxPlayersAmount));
            int minpa = (int)calculator.UpdatedSingleValue(typeof(MinPlayersAmount));
            float mauc = calculator.UpdatedSingleValue(typeof(MiningAUCoef));
            float aupa = calculator.UpdatedSingleValue(typeof(AUPartyAmount));
            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float eun = calculator.UpdatedSingleValue(typeof(EventUsabilityNormalisation));

            float averagePlayersAmount = (maxpa - minpa + 1) / 2;
            float miningBonusMultiplier = aupa * mauc * averagePlayersAmount / cna;

            foreach (EventCard card in cards) {
                card.weight = 0;
                float relationsWeight = RelationsWeight(card, brw, arw, frw, fbw);
                float noramalisedUsability = 1 + (card.usability - 1) * eun;
                card.weight += relationsWeight * ars * eip * noramalisedUsability;
                card.weight += card.stabilityIncrement * eip;
                card.weight += card.provideArtifact ? aw * am : 0;
                card.weight += card.miningBonus * miningBonusMultiplier;
            }
        }

        private float RelationsWeight (EventCard card, float brw, float arw, float frw, float fbw)
        {
            Func<EventRelation, bool> basePredicate = r => r.direction == RelationDirection.back && r.type != RelationType.reason;
            Func<EventRelation, bool> backReasonPredicate = r => r.direction == RelationDirection.back && r.type == RelationType.reason;
            Func<EventRelation, bool> frontReasonPredicate = r => r.direction == RelationDirection.front && r.type == RelationType.reason;
            Func<EventRelation, bool> frontBlockPredicate = r => r.direction == RelationDirection.front && r.type == RelationType.blocker;

            int baseAmount = card.relations.Where(basePredicate).Count();
            int backReasonsAmount = card.relations.Where(backReasonPredicate).Count();
            int frontReasonAmount = card.relations.Where(frontReasonPredicate).Count();
            int frontBlockAmount = card.relations.Where(frontBlockPredicate).Count();

            float weight = baseAmount * brw + frontReasonAmount * frw + frontBlockAmount * fbw;
            if (backReasonsAmount > 0)
            {
                weight += brw;
                weight += (backReasonsAmount - 1) * arw;
            }

            return weight;
        }

        private void AddArtifacts(List<EventCard> cards, Calculator calculator)
        {
            float ar = calculator.UpdatedSingleValue(typeof(ArtifactsRarity));
            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));

            int amount = (int)Math.Round(cna * ar, MidpointRounding.AwayFromZero);
            var ordered = cards.OrderBy(c => c.weight).ToList();
            var step = (int)Math.Floor(cards.Count() / 2f / amount);

            for (int i = 0; i < amount; i++)
                ordered[i*step].provideArtifact = true;
        }

        private void AddStabilityIncrement(List<EventCard> cards, Calculator calculator)
        {
            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float[] si_allocation = calculator.UpdatedArrayValue(typeof(StabilityIncrementAllocation));

            int[] si_amounts = AmountsForAllocation(cna, si_allocation);
            if (!calculationReport.IsSucces) return;

            var ordered = cards.OrderBy(c => c.weight).Reverse().ToList();
            var groups = SplitForAmounts(ordered, si_amounts);

            for (int incrementIter = 0; incrementIter < groups.Count() ; incrementIter++)
            {
                foreach (EventCard card in groups[incrementIter])
                {
                    int index = groups[incrementIter].IndexOf(card);
                    int stabilityIncrement = SpreadValue(incrementIter, index, si_amounts);
                    card.stabilityIncrement = stabilityIncrement;
                    si_amounts[stabilityIncrement]--;
                }
            }
        }

        private void AddMiningBonuses(List<EventCard> cards, Calculator calculator)
        {
            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));
            float[] mb_allocation = calculator.UpdatedArrayValue(typeof(EventMiningBonusAllocation));

            int[] mb_amounts = AmountsForAllocation(cna, mb_allocation);
            if (!calculationReport.IsSucces) return;

            var ordered = cards.OrderBy(c => c.weight).Reverse().ToList();
            var groups = SplitForAmounts(ordered, mb_amounts);

            for (int incrementIter = 0; incrementIter < groups.Count(); incrementIter++)
            {
                foreach (EventCard card in groups[incrementIter])
                {
                    int index = groups[incrementIter].IndexOf(card);
                    int miningBonus = SpreadValue(incrementIter, index, mb_amounts);
                    card.miningBonus = miningBonus;
                    mb_amounts[miningBonus]--;
                }
            }
        }

        private void AddBranchPoints(List<EventCard> cards, Calculator calculator)
        {
            float[] bpta = calculator.UpdatedArrayValue(typeof(BrachPointsTemplatesAllocation));
            float cna = calculator.UpdatedSingleValue(typeof(ContinuumNodesAmount));

            var templates = new BrachPointsTemplatesAllocation().templates;
            int[] amounts = AmountsForAllocation(cna, bpta);
            Dictionary<BranchPointsTemplate, int> templateAmount = new Dictionary<BranchPointsTemplate, int>();
            for (int i = 0; i < amounts.Length; i++)
            {
                var template = templates[i];
                templateAmount[template] = amounts[i];
            }

            var filteredAmounts = amounts.Where(i => i > 0).OrderBy(i => i).ToArray();
            int minAmount = filteredAmounts.Count() > 0 ? filteredAmounts[0] : 0;
            if (minAmount == 0)
            {
                calculationReport.Failed(zeroTemplatesIssues);
                return;
            }

            int templateIndex = 0;
            int amountCounter = 0;

            Dictionary<BranchPointsTemplate, List<EventCard>> templateCards = new Dictionary<BranchPointsTemplate, List<EventCard>>();
            foreach (var template in templates)
                templateCards[template] = new List<EventCard>();

            foreach (var card in cards)
            {
                var template = templates[templateIndex];
                int amount = templateAmount[template];

                // Select next template if necessary
                int checksCounter = 0;
                while (templateCards[template].Count() == templateAmount[template] 
                    || amountCounter >= amount / minAmount)
                {
                    if (checksCounter == templates.Count())
                        break;

                    templateIndex = templateIndex < templates.Count() - 1 ? templateIndex + 1 : 0;
                    template = templates[templateIndex];
                    amountCounter = 0;

                    checksCounter++;
                }

                templateCards[template].Add(card);
                amountCounter++;
            }

            foreach (var template in templates)
            {
                var tCards = templateCards[template];
                var sequence = SequenceForTemplate(template, tCards.Count(), calculator);
                for (int i = 0; i < tCards.Count(); i++)
                    tCards[i].branchPoints = sequence[i];
            }
        }

        private List<BranchPointsSet> SequenceForTemplate(BranchPointsTemplate template, int lenght, Calculator calculator)
        {
            var sequence = new List<BranchPointsSet>();
            int branchesAmount = template.failed.Count() + template.success.Count();
            switch (branchesAmount)
            {
                case 0:
                    sequence = EmptySetsSequence(lenght);
                    break;

                case 1:
                    sequence = SingleBranchSequence(template, lenght, calculator);
                    break;
                
                case 2:
                    sequence = DoubleBranchSequence(template, lenght, calculator);
                    break;
            }

            return sequence;
        }

        private List<BranchPointsSet> EmptySetsSequence(int lenght)
        {
            var sequence = new List<BranchPointsSet>();
            for (int i = 0; i < lenght; i++)
                sequence.Add(new BranchPointsSet(null, null));

            return sequence;
        }

        private List<BranchPointsSet> SingleBranchSequence(BranchPointsTemplate template, int lenght, Calculator calculator)
        {
            int mpa = (int)calculator.UpdatedSingleValue(typeof(MaxPlayersAmount));

            var sequence = new List<BranchPointsSet>();
            int index = 0;
            for (int i = 0; i < lenght; i++)
            {
                int halfCount = (int)Math.Round(mpa / 2.0f, MidpointRounding.AwayFromZero);
                var normalisedIndex = index % 2 == 0 ? index / 2 : halfCount + index / 2;
                var set = template.SetupByBranches(new int[]{normalisedIndex});
                sequence.Add(set);
                index = index == mpa - 1 ? 0 : index + 1;
            }

            return sequence;
        }

        private List<BranchPointsSet> DoubleBranchSequence(BranchPointsTemplate template, int lenght, Calculator calculator)
        {
            var bpa_std = (BranchPointsAllocation)calculator.UpdatedParameter<BranchPointsAllocation_Standard>();
            var bpa_sym = (BranchPointsAllocation)calculator.UpdatedParameter<BranchPointsAllocation_Symmetric>();

            var sequence = new List<BranchPointsSet>();
            var activeAllocation = bpa_std;
            int index = 0;
            for (int i = 0; i < lenght; i++)
            {
                int halfCount = (int)Math.Round(activeAllocation.values.Count() / 2.0f, MidpointRounding.AwayFromZero);
                var normalisedIndex = index % 2 == 0 ? index / 2: halfCount + index / 2;
                var pair = activeAllocation.values[normalisedIndex];
                var branches = new int[] {pair.Item1, pair.Item2};
                var set = template.SetupByBranches(branches);
                sequence.Add(set);

                index++;
                if (index == activeAllocation.values.Count())
                {
                    activeAllocation = activeAllocation == bpa_std ? bpa_sym : bpa_std;
                    index = 0;
                }
            }

            return sequence;
        }

        private Dictionary<int, List<EventCard>> SplitForAmounts (List<EventCard> cards, int[] amounts)
        {
            var groups = new Dictionary<int, List<EventCard>>();
            int cardIter = 0;
            for (int i = 0; i < amounts.Count(); i++)
            {
                groups.Add(i, new List<EventCard>());
                for (int j = 0; j < amounts[i]; j++)
                    groups[i].Add(cards[cardIter + j]);

                cardIter += amounts[i];
            }

            return groups;
        }

        private int[] AmountsForAllocation (float cna, float[] allocation)
        {
            int[] amounts = new int[allocation.Count()];
            for (int i = 0; i < allocation.Count(); i++)
            {
                var amount = cna * allocation[i] / allocation.Sum();
                amounts[i] = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
            }

            if (amounts.Sum() != cna)
            {
                calculationReport.Failed(roundingIssue);
                return null;
            }

            return amounts;
        }

        private int SpreadValue(int defaultValue, int index, int[] amounts)
        {
            switch (index % 3)
            {
                case 0:
                    if (amounts[defaultValue] > 0)
                        return defaultValue;
                    else
                        return NearestAvailableValue(defaultValue, index % 2 == 0, amounts);
                case 1:
                    return NearestAvailableValue(defaultValue, false, amounts);
                case 2:
                    return NearestAvailableValue(defaultValue, true, amounts);
            }

            return 0;
        }

        private int NearestAvailableValue(int current, bool nextFirst, int[] amounts)
        {
            List<int> sequence = new List<int>();

            for (int i = 1; i < amounts.Count(); i++)
            {
                int rightIndex = current + i;
                int leftIndex = current - i;

                if (nextFirst)
                {
                    if (rightIndex < amounts.Count())
                        sequence.Add(rightIndex);
                    if (leftIndex >= 0)
                        sequence.Add(leftIndex);
                }
                else
                {
                    if (leftIndex >= 0)
                        sequence.Add(leftIndex);
                    if (rightIndex < amounts.Count())
                        sequence.Add(rightIndex);
                }
            }

            foreach (int stabilityIncrement in sequence)
                if (amounts[stabilityIncrement] > 0)
                    return stabilityIncrement;

            return 0;
        }

    }
}