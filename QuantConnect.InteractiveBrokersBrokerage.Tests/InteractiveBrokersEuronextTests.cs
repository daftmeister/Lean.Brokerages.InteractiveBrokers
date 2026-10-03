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

using System;
using System.Collections.Generic;
using System.Reflection;
using IBApi;
using NUnit.Framework;
using QuantConnect.Brokerages;
using QuantConnect.Brokerages.InteractiveBrokers;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;
using IB = QuantConnect.Brokerages.InteractiveBrokers.Client;

namespace QuantConnect.Tests.Brokerages.InteractiveBrokers
{
    /// <summary>
    /// Routing of the Euronext stocks, indices and index futures, which doesn't require a connection to IB
    /// </summary>
    [TestFixture]
    public class InteractiveBrokersEuronextTests
    {
        private static readonly FieldInfo SymbolMapperField =
            typeof(InteractiveBrokersBrokerage).GetField("_symbolMapper", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CreateContractMethod =
            typeof(InteractiveBrokersBrokerage).GetMethod("CreateContract", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo MapSymbolMethod =
            typeof(InteractiveBrokersBrokerage).GetMethod("MapSymbol", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CanSubscribeMethod =
            typeof(InteractiveBrokersBrokerage).GetMethod("CanSubscribe", BindingFlags.Static | BindingFlags.NonPublic);

        private InteractiveBrokersBrokerage _brokerage;

        [SetUp]
        public void SetUp()
        {
            _brokerage = new InteractiveBrokersBrokerage();
            SymbolMapperField.SetValue(_brokerage, new InteractiveBrokersSymbolMapper(Composer.Instance.GetPart<IMapFileProvider>()));
        }

        [TestCase("MC", Market.EuronextParis, "SBF", "EUR")]
        [TestCase("ASML", Market.EuronextAmsterdam, "AEB", "EUR")]
        [TestCase("ABI", Market.EuronextBrussels, "ENEXT.BE", "EUR")]
        [TestCase("RYA", Market.EuronextDublin, "ISED", "EUR")]
        [TestCase("ENEL", Market.EuronextMilan, "BVME", "EUR")]
        [TestCase("EDP", Market.EuronextLisbon, "BVL", "EUR")]
        public void CreatesEquityContract(string ticker, string market, string expectedPrimaryExchange, string expectedCurrency)
        {
            var contract = CreateContract(Symbol.Create(ticker, SecurityType.Equity, market));

            Assert.AreEqual(ticker, contract.Symbol);
            Assert.AreEqual(IB.SecurityType.Stock, contract.SecType);
            Assert.AreEqual("Smart", contract.Exchange);
            Assert.AreEqual(expectedPrimaryExchange, contract.PrimaryExch);
            Assert.AreEqual(expectedCurrency, contract.Currency);
        }

        [TestCase(Futures.Indices.CAC40, Market.EuronextParis, "CAC40", "MONEP", "10")]
        [TestCase(Futures.Indices.CAC40Mini, Market.EuronextParis, "CAC40", "MONEP", "1")]
        [TestCase(Futures.Indices.AEX, Market.EuronextAmsterdam, "EOE", "FTA", "200")]
        [TestCase(Futures.Indices.BEL20, Market.EuronextBrussels, "BFX", "BELFOX", "10")]
        [TestCase(Futures.Indices.FTSEMIB, Market.EuronextMilan, "FTMIB", "IDEM", "5")]
        public void CreatesFutureContract(string ticker, string market, string expectedSymbol, string expectedExchange, string expectedMultiplier)
        {
            var symbol = Symbol.CreateFuture(ticker, market, new DateTime(2026, 12, 18));
            var contract = CreateContract(symbol);

            Assert.AreEqual(expectedSymbol, contract.Symbol);
            Assert.AreEqual(IB.SecurityType.Future, contract.SecType);
            Assert.AreEqual(expectedExchange, contract.Exchange);
            Assert.AreEqual(expectedMultiplier, contract.Multiplier);
            Assert.AreEqual("EUR", contract.Currency);
            // the trading class tells apart the contracts sharing the same IB symbol
            Assert.AreEqual(ticker, contract.TradingClass);
            Assert.AreEqual("20261218", contract.LastTradeDateOrContractMonth);

            // and the contract maps back to the same LEAN symbol
            Assert.AreEqual(symbol, MapSymbol(contract));
        }

        [Test]
        public void DoesNotSetTradingClassForOtherFutures()
        {
            var contract = CreateContract(Symbol.CreateFuture(Futures.Indices.EuroStoxx50, Market.EUREX, new DateTime(2026, 12, 18)));

            Assert.AreEqual("ESTX50", contract.Symbol);
            Assert.IsNull(contract.TradingClass);
        }

        [TestCase("CAC40", Market.EuronextParis, "CAC40", "MONEP")]
        [TestCase("AEX", Market.EuronextAmsterdam, "EOE", "FTA")]
        [TestCase("BEL20", Market.EuronextBrussels, "BFX", "BELFOX")]
        [TestCase("FTSEMIB", Market.EuronextMilan, "FTMIB", "IDEM")]
        public void CreatesIndexContract(string ticker, string market, string expectedSymbol, string expectedExchange)
        {
            var symbol = Symbol.Create(ticker, SecurityType.Index, market);
            var contract = CreateContract(symbol);

            Assert.AreEqual(expectedSymbol, contract.Symbol);
            Assert.AreEqual(IB.SecurityType.Index, contract.SecType);
            Assert.AreEqual(expectedExchange, contract.Exchange);
            Assert.IsTrue(CanSubscribe(symbol));
        }

        // portfolio updates provide the primary exchange in the exchange field
        [TestCase("MC", "SBF", "", "EUR", Market.EuronextParis)]
        [TestCase("ASML", "AEB", "", "EUR", Market.EuronextAmsterdam)]
        [TestCase("ABI", "ENEXT.BE", "", "EUR", Market.EuronextBrussels)]
        [TestCase("RYA", "ISED", "", "EUR", Market.EuronextDublin)]
        [TestCase("ENEL", "BVME", "", "EUR", Market.EuronextMilan)]
        [TestCase("EDP", "BVL", "", "EUR", Market.EuronextLisbon)]
        [TestCase("EQNR", "OSE", "", "NOK", Market.EuronextOslo)]
        // open orders provide it in the primary exchange field
        [TestCase("MC", "SMART", "SBF", "EUR", Market.EuronextParis)]
        // executions provide the execution venue
        [TestCase("EQNR", "CHIXEN", "", "NOK", Market.EuronextOslo)]
        [TestCase("AAPL", "NASDAQ", "", "USD", Market.USA)]
        [TestCase("AAPL", "SMART", "NASDAQ", "USD", Market.USA)]
        public void MapsEquityContract(string ticker, string exchange, string primaryExchange, string currency, string expectedMarket)
        {
            var contract = new Contract
            {
                Symbol = ticker,
                SecType = IB.SecurityType.Stock,
                Exchange = exchange,
                PrimaryExch = primaryExchange,
                Currency = currency
            };

            var symbol = MapSymbol(contract);

            Assert.AreEqual(ticker, symbol.Value);
            Assert.AreEqual(SecurityType.Equity, symbol.SecurityType);
            Assert.AreEqual(expectedMarket, symbol.ID.Market);
        }

        [TestCase("CAC40", "FCE", "MONEP", Futures.Indices.CAC40, Market.EuronextParis)]
        [TestCase("CAC40", "MFC", "MONEP", Futures.Indices.CAC40Mini, Market.EuronextParis)]
        [TestCase("EOE", "FTI", "FTA", Futures.Indices.AEX, Market.EuronextAmsterdam)]
        [TestCase("BFX", "BXF", "BELFOX", Futures.Indices.BEL20, Market.EuronextBrussels)]
        [TestCase("FTMIB", "FIB", "IDEM", Futures.Indices.FTSEMIB, Market.EuronextMilan)]
        [TestCase("FTMIB", "MINI", "IDEM", Futures.Indices.FTSEMIBMini, Market.EuronextMilan)]
        public void MapsFutureContract(string ibSymbol, string tradingClass, string exchange, string expectedTicker, string expectedMarket)
        {
            var contract = new Contract
            {
                Symbol = ibSymbol,
                SecType = IB.SecurityType.Future,
                Exchange = exchange,
                TradingClass = tradingClass,
                Currency = "EUR",
                LastTradeDateOrContractMonth = "20261218"
            };

            var symbol = MapSymbol(contract);

            Assert.AreEqual(expectedTicker, symbol.ID.Symbol);
            Assert.AreEqual(expectedMarket, symbol.ID.Market);
            Assert.AreEqual(new DateTime(2026, 12, 18), symbol.ID.Date);
        }

        [TestCase("CAC40", "MFC", true, Futures.Indices.CAC40Mini)]
        [TestCase("FTMIB", "FIB", true, Futures.Indices.FTSEMIB)]
        // unsupported contracts sharing the IB symbol, like the Mini AEX or the Micro FTSE MIB
        [TestCase("EOE", "MFA", false, null)]
        [TestCase("FTMIB", "MICR", false, null)]
        // the trading class must belong to the IB symbol
        [TestCase("EOE", "FCE", false, null)]
        [TestCase("ESTX50", "FESX", false, null)]
        [TestCase("CAC40", "", false, null)]
        public void IdentifiesFuturesByTradingClass(string ibSymbol, string tradingClass, bool expected, string expectedTicker)
        {
            Assert.AreEqual(expected, InteractiveBrokersSymbolMapper.TryGetLeanFutureRootSymbol(ibSymbol, tradingClass, out var ticker));
            Assert.AreEqual(expectedTicker, ticker);
        }

        [TestCase(Market.EuronextParis, "MONEP")]
        [TestCase(Market.EuronextAmsterdam, "FTA")]
        [TestCase(Market.EuronextBrussels, "BELFOX")]
        [TestCase(Market.EuronextMilan, "IDEM")]
        public void RoutesFuturesToExchange(string market, string expectedExchange)
        {
            Assert.AreEqual(expectedExchange, InteractiveBrokersBrokerage.GetSymbolExchange(SecurityType.Future, market));
        }

        [TestCase(Market.EuronextParis)]
        [TestCase(Market.EuronextAmsterdam)]
        [TestCase(Market.EuronextBrussels)]
        [TestCase(Market.EuronextDublin)]
        [TestCase(Market.EuronextMilan)]
        [TestCase(Market.EuronextLisbon)]
        public void CanSubscribeToEquities(string market)
        {
            Assert.IsTrue(CanSubscribe(Symbol.Create("ABC", SecurityType.Equity, market)));
        }

        private static IEnumerable<TestCaseData> UnsupportedSymbols
        {
            get
            {
                yield return new TestCaseData(Symbol.Create("EQNR", SecurityType.Equity, Market.EuronextOslo)).SetName("OsloEquity");
                yield return new TestCaseData(Symbol.Create("OBX", SecurityType.Index, Market.EuronextOslo)).SetName("OsloIndex");
                yield return new TestCaseData(Symbol.CreateFuture(Futures.Indices.OBX, Market.EuronextOslo, new DateTime(2026, 12, 18))).SetName("OsloFuture");
                yield return new TestCaseData(Symbol.CreateFuture(Futures.Indices.FTSEMIBMini, Market.EuronextMilan, new DateTime(2026, 12, 18))).SetName("MiniFTSEMIBFuture");
                yield return new TestCaseData(Symbol.Create(Futures.Indices.FTSEMIBMini, SecurityType.Future, Market.EuronextMilan)).SetName("MiniFTSEMIBCanonicalFuture");
                yield return new TestCaseData(Symbol.CreateOption(Symbol.CreateFuture(Futures.Indices.FTSEMIBMini, Market.EuronextMilan, new DateTime(2026, 12, 18)),
                    Market.EuronextMilan, OptionStyle.European, OptionRight.Call, 40000m, new DateTime(2026, 12, 18))).SetName("MiniFTSEMIBFutureOption");
            }
        }

        [TestCaseSource(nameof(UnsupportedSymbols))]
        public void RejectsUnsupportedSymbols(Symbol symbol)
        {
            Assert.IsTrue(InteractiveBrokersBrokerage.IsUnsupportedSymbol(symbol, out var reason));
            StringAssert.Contains("not supported yet", reason);

            // no IB contract, so neither orders nor data requests can reach IB
            var exception = Assert.Throws<TargetInvocationException>(() => CreateContract(symbol));
            Assert.IsInstanceOf<NotSupportedException>(exception.InnerException);
            Assert.IsFalse(CanSubscribe(symbol));

            var messages = new List<BrokerageMessageEvent>();
            _brokerage.Message += (_, e) => messages.Add(e);

            var order = new MarketOrder(symbol, 1, new DateTime(2026, 10, 5));
            Assert.IsFalse(_brokerage.PlaceOrder(order));
            Assert.IsFalse(_brokerage.UpdateOrder(order));
            CollectionAssert.IsEmpty(_brokerage.LookupSymbols(symbol, false));

            Assert.AreEqual(3, messages.Count);
            Assert.That(messages, Has.All.Matches<BrokerageMessageEvent>(m => m.Code == "UnsupportedSymbol"));
        }

        [TestCase("MC", SecurityType.Equity, Market.EuronextParis)]
        [TestCase("CAC40", SecurityType.Index, Market.EuronextParis)]
        [TestCase("ENEL", SecurityType.Equity, Market.EuronextMilan)]
        // Lean's Osaka market, unrelated to Euronext Oslo
        [TestCase("N225", SecurityType.Index, Market.OSE)]
        public void DoesNotRejectSupportedSymbols(string ticker, SecurityType securityType, string market)
        {
            Assert.IsFalse(InteractiveBrokersBrokerage.IsUnsupportedSymbol(Symbol.Create(ticker, securityType, market), out _));
        }

        [TestCase(Futures.Indices.FTSEMIB, Market.EuronextMilan)]
        [TestCase(Futures.Indices.CAC40Mini, Market.EuronextParis)]
        public void DoesNotRejectSupportedFutures(string ticker, string market)
        {
            Assert.IsFalse(InteractiveBrokersBrokerage.IsUnsupportedSymbol(Symbol.CreateFuture(ticker, market, new DateTime(2026, 12, 18)), out _));
        }

        [Test]
        public void SupportedOrderIsNotRejectedAsUnsupported()
        {
            var messages = new List<BrokerageMessageEvent>();
            _brokerage.Message += (_, e) => messages.Add(e);

            // the brokerage isn't connected, so the order fails for that reason only
            Assert.IsFalse(_brokerage.PlaceOrder(new MarketOrder(Symbol.Create("MC", SecurityType.Equity, Market.EuronextParis), 1, new DateTime(2026, 10, 5))));
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("PlaceOrderWhenDisconnected", messages[0].Code);
        }

        [Test]
        public void ParsesMalformedFutureContractByTradingClass()
        {
            var mapper = new InteractiveBrokersSymbolMapper(Composer.Instance.GetPart<IMapFileProvider>());
            var malformedContract = new Contract
            {
                Symbol = "CAC40    DEC2026",
                SecType = IB.SecurityType.Future,
                Exchange = "MONEP",
                Currency = "EUR",
                Multiplier = "1",
                TradingClass = "MFC",
                LastTradeDateOrContractMonth = "0"
            };

            var contract = mapper.ParseMalformedContractFutureSymbol(malformedContract, SymbolPropertiesDatabase.FromDataFolder());

            Assert.AreEqual("CAC40", contract.Symbol);
            Assert.AreEqual("MFC", contract.TradingClass);
            // 3rd Friday of December 2026
            Assert.AreEqual("20261218", contract.LastTradeDateOrContractMonth);
            Assert.AreEqual(Symbol.CreateFuture(Futures.Indices.CAC40Mini, Market.EuronextParis, new DateTime(2026, 12, 18)), MapSymbol(contract));
        }

        private Contract CreateContract(Symbol symbol)
        {
            return (Contract)CreateContractMethod.Invoke(_brokerage, new object[] { symbol, false, null, null });
        }

        private Symbol MapSymbol(Contract contract)
        {
            return (Symbol)MapSymbolMethod.Invoke(_brokerage, new object[] { contract });
        }

        private static bool CanSubscribe(Symbol symbol)
        {
            return (bool)CanSubscribeMethod.Invoke(null, new object[] { symbol });
        }
    }
}
