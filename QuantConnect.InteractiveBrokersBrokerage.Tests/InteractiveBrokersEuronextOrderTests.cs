/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages.InteractiveBrokers;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Linq;

namespace QuantConnect.Tests.Brokerages.InteractiveBrokers
{
    /// <summary>
    /// Euronext Paris stock (LVMH) and CAC 40 future orders, to be run during the Euronext trading hours
    /// </summary>
    [TestFixture]
    [Explicit("These tests require the IBGateway to be installed.")]
    public class InteractiveBrokersEuronextOrderTests : BrokerageTests
    {
        private static readonly Symbol LvmhSymbol = Symbol.Create("MC", SecurityType.Equity, Market.EuronextParis);
        private static readonly Symbol Cac40FutureSymbol = TestUtils.GetFutureContracts(
            Symbol.Create(Futures.Indices.CAC40, SecurityType.Future, Market.EuronextParis), 1).Single();

        protected override Symbol Symbol => LvmhSymbol;
        protected override SecurityType SecurityType => Symbol.SecurityType;

        private static TestCaseData[] OrderTestParameters()
        {
            return new[]
            {
                new TestCaseData(new MarketOrderTestParameters(LvmhSymbol)),
                new TestCaseData(new LimitOrderTestParameters(LvmhSymbol, 10000m, 10m)),
                new TestCaseData(new StopMarketOrderTestParameters(LvmhSymbol, 10000m, 10m)),
                new TestCaseData(new MarketOrderTestParameters(Cac40FutureSymbol)),
                new TestCaseData(new LimitOrderTestParameters(Cac40FutureSymbol, 20000m, 1000m)),
                new TestCaseData(new StopMarketOrderTestParameters(Cac40FutureSymbol, 20000m, 1000m)),
            };
        }

        [Test, TestCaseSource(nameof(OrderTestParameters))]
        public override void CancelOrders(OrderTestParameters parameters)
        {
            base.CancelOrders(parameters);
        }

        [Test, TestCaseSource(nameof(OrderTestParameters))]
        public override void LongFromZero(OrderTestParameters parameters)
        {
            base.LongFromZero(parameters);
        }

        [Test, TestCaseSource(nameof(OrderTestParameters))]
        public override void CloseFromLong(OrderTestParameters parameters)
        {
            base.CloseFromLong(parameters);
        }

        [Test, TestCaseSource(nameof(OrderTestParameters))]
        public override void ShortFromZero(OrderTestParameters parameters)
        {
            base.ShortFromZero(parameters);
        }

        [Test, TestCaseSource(nameof(OrderTestParameters))]
        public override void CloseFromShort(OrderTestParameters parameters)
        {
            base.CloseFromShort(parameters);
        }

        protected override bool IsAsync()
        {
            return true;
        }

        protected override decimal GetAskPrice(Symbol symbol)
        {
            return 1m;
        }

        protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
        {
            return new InteractiveBrokersBrokerage(new QCAlgorithm(), orderProvider, securityProvider);
        }

        protected override void DisposeBrokerage(IBrokerage brokerage)
        {
            if (brokerage != null)
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }
    }
}
