# Marius MultiAsset DEMO: Gold, Silber, Bitcoin, Ethereum

**Experimenteller Demobot.** Kompilierung via GitHub Actions, aber keine nachgewiesene Rentabilität und noch kein Forward-Test bei Pepperstone. Regelbasierter cBot, KEINE selbstlernende KI.

## Funktionsumfang
- Eine einzige Demo-Instanz beobachtet Gold (XAUUSD), Silber (XAGUSD), Bitcoin (BTCUSD) und Ethereum (ETHUSD) auf M5. Erkennt die Standard-Symbole automatisch und auch eindeutige Broker-Suffixe; nur bei mehreren möglichen Treffern einmal exakten Symbolnamen einstellen.
- EMA 9/21, RSI 14, ATR-Stop-Loss mit Take-Profit beim 1,5-Fachen des anfänglichen Stopabstands. Long und Short.
- Keine feste Anzahl von Trades am Tag; maximal zwei Positionen gleichzeitig und eine Position je Instrument. Standardrisiko 0,75 % des Kontostands je Position, maximal 1 %.
- Tagesstopp bei -5 % und Gewinnstopp bei +10 % oder 10 Einheiten Kontowährung (was zuerst erreicht wird). Mit einem 100-EUR-Demokonto anfangs -5 EUR und +10 EUR. Gewinne oder Grenzen sind niemals garantiert.
- Sicherheitsblockierung aller Echtgeldkonten. Demo-Orders sind standardmäßig aktiviert (nur DEMO, echte Konten programmatisch blockiert).
- Eigenständige Prüfungen von Mindestvolumen, Mindest-Margin und geschätztem Stop-Loss-Risiko. KEINE erzwungenen 0,01 Lot.
- Journal zeigt verständliche Meldungen wie MIN LOT NOT AFFORDABLE, NO SIGNAL, SIGNAL-ONLY, ORDER REJECTED und HALT.
- Handelszeiten standardmäßig 07 bis 21 UTC; Kryptowährungen optional auch am Wochenende, sofern der Broker sie anbietet. Am Sitzungsende versucht der Bot, offene Bot-Positionen zu schließen.

## Installation mit iPad/iPhone

1. Die .algo-Datei aus dem erfolgreichen GitHub-Actions-Artefakt herunterladen und bei Bedarf die ZIP-Datei entpacken.
2. cTrader Mobile öffnen und mit dem **Pepperstone-Demokonto** anmelden.
3. Unter Algo / cBots auf Upload tippen und die Datei auswählen. Alternativ die .algo-Datei in iOS Dateien öffnen und cTrader wählen.
4. Den neuen MariusMultiAssetDemo-Bot auswählen und eine Cloud-Instanz im **Demokonto** anlegen. Das gewählte Hauptchartsymbol ist nicht entscheidend: Der Bot lädt alle vier Märkte selbst.
5. Die Symbolnamen werden automatisch erkannt, sofern sie exakt oder mit eindeutigem Broker-Suffix existieren. Nur bei Meldung MISSING/AMBIGUOUS SYMBOL musst du den Namen einmal korrigieren.
6. ACHTUNG: Enable DEMO orders ist standardmäßig TRUE; nach dem Start kann der Bot automatisch Demo-Orders platzieren. Für einen reinen Diagnoselauf einmal FALSE setzen und nach Prüfung wieder TRUE.
7. Läuft der alte Goldbot noch in der Cloud, diesen vorher stoppen: In einem Demo-Cloud-Konto ist normalerweise nur eine Instanz gleichzeitig erlaubt. Nur die neue Instanz starten.
8. Falls MIN LOT NOT AFFORDABLE erscheint, reicht das Kapital für die Mindestposition innerhalb unserer Risikovorgaben nicht. Die Positionsgröße NICHT ungeprüft auf 0,01 Lot festnageln. Für einen Demotest darfst du das virtuelle Guthaben ggf. auf 1.000 EUR erhöhen.
9. Wenn Logs und Margin plausibel sind, Instanz stoppen, Enable DEMO orders TRUE setzen, Instanz wieder starten. Erst dann kann sie Demo-Orders senden.
10. Täglich Journal und Positionen kontrollieren. NO SIGNAL bedeutet, dass kein Signal entstanden ist. ORDER REJECTED zeigt den Fehler des Brokers.
11. Eine Demo-Cloud-Instanz stoppt spätestens nach sieben Tagen; sie muss dann neu gestartet werden. Bot gestoppt bedeutet NICHT automatisch alle Positionen geschlossen.

## Einschränkungen
- Kein Bot garantiert zehn Euro pro Tag. Tagesstopp -5 % ist ein Software-Grenzwert, kein harter Verlustschutz bei Slippage/Gaps/Serverausfällen.
- Die Tagesbilanz wird aus dem Konto, den geschlossenen BOT-Positionen und offenem Bot-P&L rekonstruiert. Das setzt ein dediziertes Demokonto OHNE Fremdtrades und Ein-/Auszahlungen voraus.
- cTrader Cloud bewahrt LocalStorage nach einem Neustart nicht auf; Tages-Haltzustände und Historie können in Sonderfällen voneinander abweichen. Reale Kontostände beobachten.
- Kein KI-Nachrichtenmodul und keine automatische Selbstoptimierung. Nicht mit echtem Geld verwenden. Erst Backtest und längeren Demo-Forward-Test durchführen.
- Keine Zugangsdaten in dieses öffentliche GitHub-Projekt schreiben.

## Dateien
- MariusMultiAssetDemo.cs: C# Quellcode
- MariusMultiAssetDemo.csproj: .NET-Projekt
- .github/workflows/multiasset-build.yml: Automatischer Build mit .algo-Artefakt

Offizielle Hilfe: https://help.ctrader.com/cbots/de/how-tos/install-a-cbot/
Cloud-Hilfe: https://help.ctrader.com/ctrader-algo/mobile-algo/
GitHub Actions: https://github.com/Marius040908/Marius-Gold/actions/workflows/multiasset-build.yml
