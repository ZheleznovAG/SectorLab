using System;
using NUnit.Framework;
using SectorLab.Battle;

namespace SectorLab.Tests
{
    public class MatchForecastTests
    {
        [Test]
        public void NobodyIsAheadAtTheStart()
        {
            Assert.That(MatchRules.Forecast(0, 0, 0, 0, 180), Is.EqualTo(BattleOutcome.Draw));
        }

        [Test]
        public void IncomeDecidesTheForecastBeforeTheScoreDoes()
        {
            // Behind 10:20, but earning 2/s against 0: reaches 45 in 17.5 s.
            Assert.That(MatchRules.Forecast(10, 20, 2, 0, 120), Is.EqualTo(BattleOutcome.Victory));
            Assert.That(MatchRules.Forecast(20, 10, 0, 1, 120), Is.EqualTo(BattleOutcome.Defeat));
        }

        [Test]
        public void FirstToReachTheLimitWinsTheForecast()
        {
            // Player needs 15 s at 1/s; enemy needs 10 s at 2/s.
            Assert.That(MatchRules.Forecast(30, 25, 1, 2, 100), Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(MatchRules.Forecast(35, 25, 1, 1, 100), Is.EqualTo(BattleOutcome.Victory));
            Assert.That(MatchRules.Forecast(35, 35, 1, 1, 100), Is.EqualTo(BattleOutcome.Draw));
        }

        [Test]
        public void WithoutReachingTheLimitTheTimeoutScoreDecides()
        {
            Assert.That(MatchRules.Forecast(12, 10, 0, 0, 30), Is.EqualTo(BattleOutcome.Victory));
            Assert.That(MatchRules.Forecast(10, 5, 0, 1, 5), Is.EqualTo(BattleOutcome.Draw));
            Assert.That(MatchRules.Forecast(10, 5, 0, 1, 4), Is.EqualTo(BattleOutcome.Victory));
            Assert.That(MatchRules.Forecast(10, 5, 0, 1, 20), Is.EqualTo(BattleOutcome.Defeat));
        }

        [Test]
        public void InvalidValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.Forecast(-1, 0, 0, 0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRules.Forecast(0, 0, double.NaN, 0, 10));
        }
    }
}
