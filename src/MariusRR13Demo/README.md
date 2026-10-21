# Marius RR13 DEMO – 1% Maximalrisiko / 3% angestrebter Gewinn pro Trade

Eine **separate experimentelle cTrader-DEMO-Strategie**, nicht das bisherige adaptive Monatsziel-Modell. Kein Echtgeldhandel: `Account.IsLive` stoppt den cBot.

## Genaue Regeln

- Kapitalbezogen: Bei **2.000 EUR Equity** riskiert eine neue Position per Stop-Loss maximal **etwa 20 EUR = 1%** vor Slippage und möglichen Gebühren.
- Der **Take-Profit liegt 3x so weit entfernt wie der Stop-Loss (3R)**; bei tatsächlich 20 EUR initialem Kursrisiko entspricht das **etwa 60 EUR Bruttoziel bzw. 3% der Equity**, vor Gebühren/Abweichungen.
- Wegen Lotgrößenrundung, Broker-Mindestpositionen, Spread, Kommissionen und Margin kann der tatsächliche Einsatz deutlich unter 1% liegen und das absolute Gewinnziel unter 3%. Bei zu hohem Mindestlot riskiert der Bot **nicht** mehr als vorgesehen, sondern überspringt den Trade.
- **Keine Tages-/Monatsgewinnlimits und kein Tages-/Monatsverluststopp.** Trades pro Tag **nicht begrenzt**. Höchstens **4 gleichzeitige Positionen**, maximal 1 je Instrument. **Kumuliertes vorgesehenes Stop-Loss-Risiko bis 4% des aktuellen Kontoeigenkapitals** (bei 2.000 EUR etwa 80 EUR) bei 4 offenen Positionen. Mehrere Verlusttrades am Tag können sich aufsummieren, auch über 4% Tagesverlust hinaus. Das ist sehr risikoreich.
- Standardmäßig Handelsfenster 7–21 Uhr UTC, Wochenendtrading ausschließlich in verfügbaren Kryptowährungs-Märkten. Kein Erzwingen von Trades ohne passende Signale oder verfügbare Margin.
- Ein *serverseitiger Stop-Loss und Take-Profit* wird **sofort bei der Ordereröffnung** hinterlegt. Ein 1%-Marktpreisverlust ist nicht dasselbe wie 1% **Kontorisiko**. Stopps sind keine Ausführungsgarantie.
- Optionales automatisches Nachziehen des Stop-Loss ab ca. 1,1 ATR und frühzeitiger Ausstieg bei bestätigter M15-Trendumkehr bleiben aus dem adaptiven Bot erhalten. Ein Trailing-Stop kann den Trade **vor** 3R schließen, um Gewinn zu schützen; der 3R-TP wird beim Verschieben des SL erhalten.
- M15: Richtung mit EMA20/EMA50, M5: Entry mit EMA9/EMA21 und RSI14. ATR14 bestimmt Stopabstand; Volumen in Broker-Einheiten wird aus geplantem Verlustbudget berechnet.
- **Lernen aus mehreren Verlusten**: getrennt für Gold, Silber, Bitcoin und Ethereum sowie Long/Short je 12 geschlossene Positionen. Ab mindestens 8 Verlustpositionen und negativer Nettosumme wird der DEFENSIVE-Modus aktiv: strengere Signalregeln und **nur noch 0,5% Kontorisiko pro neuem Trade**, daher ca. 1,5% Zielrendite bei 3R vor Kosten. Rückkehr zu Normal, sobald das rollierende Fenster die Bedingung nicht mehr erfüllt.
- Der Code ist **regelbasiert adaptiv**, kein neuronales, selbstlernendes KI-Modell. Lernhistorie wird aus der cTrader-Brokerhistorie rekonstruiert.

## Installation
1. Unter [GitHub Actions](../../actions/workflows/rr13-build.yml) den jüngsten erfolgreich abgeschlossenen Build "Build Marius RR13 DEMO" öffnen und das Artefakt `MariusRR13Demo-algo` herunterladen.
2. Die enthaltene `.algo`-Datei auf deinem iPad in Pepperstone cTrader importieren.
3. **Alte laufende adaptive Bot-Instanz stoppen** und vorhandene Positionen kontrollieren / bewusst schließen. **Nicht beide Bots auf demselben Demokonto parallel**.
4. Eine Instanz `MariusRR13Demo` im separaten Demokonto starten. Im Journal auf `RR13 DEMO`, `RISK ALLOCATION`, `SIGNAL`, `DEMO ORDER OPENED`, `MIN LOT NOT AFFORDABLE` und `LEARNING` achten.

## Risiko und Teststatus
Diese Variante ist bewusst spekulativ. Vier Positionen können zusammen 4% geplantes Anfangsrisiko bedeuten. Mehrere abgeschlossene Verlustpositionen können einen noch größeren Tagesverlust auslösen. Marktpreis-Lücken, fehlende Liquidität, Stop-Slippage, Gebühren und technische Fehler können 1% Verlust je Trade überschreiten. Auch 3R-Gewinne sind nicht gesichert. Kompiliert heißt **nicht** backgetestet oder profitabel. Vor Echtgeldhandel umfassend im Demo- und Backtest prüfen.

Die 1%- und 3%-Werte gelten für das **Konto**, nicht für prozentuale Veränderungen des Bitcoin-, Gold-, Silber- oder Ethereum-Kurses.
