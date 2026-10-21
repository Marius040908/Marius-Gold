# Marius RR13 DEMO – Flexible Risikosteuerung, maximal zwei Positionen

Eigenständiger experimenteller cTrader-cBot für das **Demokonto**. `Account.IsLive` blockiert Echtgeldhandel. Der alte adaptive Monatsziel-Bot bleibt unverändert.

## Regeln
- **Maximal zwei offene Positionen gleichzeitig**, höchstens eine je Gold (XAUUSD), Silber (XAGUSD), Bitcoin (BTCUSD), Ethereum (ETHUSD).
- **Keine feste Obergrenze für Trades pro Tag.** Signale entstehen beim Abschluss von M5-Kerzen. Nur gültige Signale werden gehandelt; es gibt keine erzwungenen Einstiege.
- **1 % der aktuellen Kontoequity ist die absolute geschätzte Obergrenze je neuem Trade**, nicht das vorgeschriebene Risiko. Bei ca. 2.000 EUR Kontowert maximal ca. 20 EUR geplantes Kursrisiko pro Position; nach Gebühren, Slippage und Kurslücken kann mehr verloren gehen.
- **Signalabhängige Risikostufen:** Der Bot prüft 4 zusätzliche Kriterien (M15-Trendstärke, M5-Trendabstand, günstiger RSI-Bereich, geringer Spread relativ zur M5-Volatilität). Je nach Anzahl ergibt sich **0,25 %, 0,50 %, 0,75 % oder höchstens 1 %** neues Positionsrisiko. Dies sind experimentelle Heuristiken, **keine nachgewiesenen Gewinnwahrscheinlichkeiten**.
- **Lernen nach mehreren Verlusten:** Separat je Markt und Kauf-/Verkaufsrichtung werden die letzten 12 abgeschlossenen Positionen gewertet. Nach mindestens 8 Verlusten **und negativem Gesamtergebnis** halbiert der Bot die Positionseinsatzgrenze für weitere Trades und verschärft die Einstiegskriterien. Keine Anpassung nach einem einzelnen Verlust.
- **Maximal 2 % gemeinsames geplantes Stop-Loss-Risiko** aller laufenden Bot-Positionen, gemessen am aktuellen Kontoeigenkapital. Keine automatische Verlustaufstockung nach schlechten Trades.
- **Kein festes Take-Profit-Limit mehr.** Ein Trade kann bei günstiger Entwicklung über 3 % oder auch 10 % Kontorendite erzielen; **weder das Erreichen noch eine solche Rendite ist wahrscheinlichkeitsbewertet oder garantiert**. 15, 25 und 40 EUR Floating-Profit werden lediglich im Journal protokolliert.
- **Dynamische Gewinnsicherung:** ATR-basierter Stop-Loss beim Öffnen. Ab ausreichender positiver Bewegung wird dieser Stop-Loss schrittweise nachgezogen. Eine bestätigte Umkehr des M15-Trends kann einen früheren Ausstieg auslösen. Stop-Loss niemals absichtlich verschlechtern.
- **Keine Tages- oder Monatsgewinnziele/-stopps**. Eine Serie vieler Verlusttrades kann einen erheblichen Tages- oder Monatsverlust verursachen. Das ist eine risikoreiche Strategie, ausschließlich für einen separaten Demotest.
- Handelsfenster standardmäßig 7–21 Uhr UTC, am Wochenende nur Kryptowährungen, soweit tatsächlich verfügbar. Nach der Sitzung versucht der Bot, seine Positionen zu schließen.

## Was „1 % Risiko“ bedeutet
Diese 1 % beziehen sich auf die **Kontoequity**, nicht auf eine Kursänderung des gehandelten Instruments. Der Bot verwendet ATR und Spread für den Stop-Abstand und rundet die berechnete Stückzahl nach Brokerregeln **ab**. Bei Mindestlots oder Marginmangel überspringt er die Order statt die Risikogrenze zu verletzen. Bei 2.000 EUR Equity entsprechen die Risikostufen ungefähr 5/10/15/20 EUR je Trade, sofern Broker-Mindestgrößen diese zulassen. Die Kontowährung ist nicht zwangsläufig EUR.

## Installation auf dem iPad
1. In GitHub Actions den neuesten erfolgreichen `Build Marius RR13 DEMO` öffnen. Artefakt `MariusRR13Demo-algo` herunterladen und die `.algo`-Datei in cTrader importieren.
2. Bestehende Bot-Instanzen auf **demselben** Demokonto stoppen und offene Positionen prüfen bzw. kontrolliert schließen; dieser Bot führt nur seine eigenen Trades.
3. Genau **eine** Instanz `MariusRR13Demo` auf einem separaten Demokonto starten. Livekonten sind hart blockiert.
4. Journal prüfen: `RISK ALLOCATION` mit Stärke, Risikostufe und Positionsanzahl, `SIGNAL`, `DEMO ORDER OPENED`, `TRAIL`, `LEARNING`, `MIN LOT NOT AFFORDABLE`.
5. Mehrere Wochen Demo und Backtests durchführen und Orderfehler, Margin, Slippage, Profits und Drawdowns auswerten. Ein erfolgreicher Build ist **kein Beweis für profitable Ergebnisse**.

## Grenzen
- Regeln reagieren nach vielen Verlusten defensiver, aber das ist **keine frei trainierende neuronale KI**.
- Bei Cloud-Neustarts sind die Broker-Positionen noch vorhanden; dynamische Trailing-Verwaltung ist nur bei laufender Instanz aktiv. Der zuletzt erfolgreich beim Broker gesetzte Stop-Loss bleibt soweit brokerseitig ausgeführt bestehen.
- 1 % geplantes Risiko ist kein garantierter Höchstverlust. Es gibt keinen systemweiten Tages-Verluststopp. Vier Märkte werden beobachtet, aber maximal zwei dürfen gleichzeitig offene Positionen haben.
- Stopps und Veränderungen wirken nur auf die Positionen mit diesem Bot-Label. Manuelle Trades und mehrere Bots auf demselben Konto beeinträchtigen die Berechnungen; deshalb getrennt testen.
