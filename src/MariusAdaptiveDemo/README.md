# Marius Adaptive DEMO v2 – Gold, Silber, Bitcoin, Ethereum

**Experimenteller cTrader-C#-Demobot – kein Profitversprechen, keine selbstlernende neuronale KI.** Neue Version, getrennt vom bestehenden MariusMultiAssetDemo. Keine Echtgeld-Orders im Code (Account.IsLive stoppt den Bot). Die einzige Startinstanz lädt alle vier Märkte selbst.

## Analyse
- **M15 (15 Minuten)**: abgeschlossene Kerze und EMA20/EMA50 bestimmen die übergeordnete Trendrichtung.
- **M5 (5 Minuten)**: EMA9/EMA21 und RSI14 bestimmen eine mögliche Long- oder Short-Einstiegsgelegenheit; ATR bestimmt den Stop-Loss.
- Kein Tageslimit für die Trade-Anzahl, aber maximal zwei gleichzeitig offene Positionen.
- Positionen nur, wenn Mindestlot, geschätztes Verlustbudget, Margin, Spread, Handelszeit und aktuelle Kontoexposition passen.
- **Neuer Plan: Monatsgewinnziel +10 %**, maximal **3 % Tagesverlust** vom rekonstruierten UTC-Tagesstartkapital. Das 10-%-Tagesziel ist vollständig entfernt.
- **Dynamisches Tagesgewinnziel**: Zu jedem UTC-Tagesstart wird die Differenz zum Monatsziel auf die restlichen Kalendertage verteilt (Zinseszinseffekt). Beispiel: 2.000 EUR am Monatsanfang, 30 Kalendertage: monatlich +200 EUR, rechnerisch täglich ca. +0,318 %. Sobald das jeweilige Tagesziel erreicht wird, pausiert der Bot bis zum nächsten UTC-Tag; sobald der Monatswert erreicht wird, pausiert er für den restlichen UTC-Monat.
- **Keine Verlustjagd**: Schlechtere Monatsentwicklung kann das berechnete Tagesziel erhöhen, jedoch niemals das Risiko oder den Hebel. Offene Bot-Positionen werden auf maximal 1,5 % geschätztes gemeinsames SL-Risiko begrenzt. Bei fehlendem Signal handeln wir nicht.

## Lernmechanismus nach mehreren Fehlern
- Bewertet für **jeden Markt und jede Richtung getrennt** die letzten **12 abgeschlossenen Positionen** dieses Bots aus dem cTrader-Handelsverlauf.
- Nur wenn **mindestens 8 der letzten 12 Trades verloren** haben UND **die Summe ihrer Nettogewinne negativ** ist, wird der Modus DEFENSIVE aktiviert.
- In DEFENSIVE werden RSI- und Momentum-Anforderungen strenger; das geschätzte Risikobudget neuer Orders wird **halbiert**. Erhöht Risiko niemals als Reaktion auf Gewinne oder Verluste.
- Mit neuen abgeschlossenen Trades wird das rollierende Fenster erneut ausgewertet; wird die Verlustbedingung nicht mehr erfüllt, kehrt die Strategie zu NORMAL zurück.
- Kein Reagieren auf einzelne Fehler, kein Nachkaufen von Verlusten, kein Martingale, kein Training eines neuronalen Netzes.
- Ergebnisse aus *früheren Bot-Versionen* fließen bewusst nicht ein, weil deren Strategie anders war. Es müssen zuerst genügend Trades dieser Version stattfinden.
- Die Lernbasis liegt in der **cTrader-Handelshistorie**, nicht im Cloud-Dateispeicher. Nach Neustart wird aus dem Brokerverlauf rekonstruiert, soweit die Historie vollständig verfügbar ist.
- Learning-Journal-Meldungen zeigen Samplezahl, Nettoergebnis, Verlustanzahl und NORMAL/DEFENSIVE, damit die Anpassung überprüfbar ist.

## Installation auf dem iPad
1. GitHub Actions "Build Marius Adaptive DEMO v2" abwarten (grüner Status).
2. Aus dem Artefakt `MariusAdaptiveDemo-v2` die `.algo`-Datei herunterladen.
3. In der iOS Dateien-App die `.algo`-Datei öffnen und in Pepperstone cTrader importieren. Alternativ über den Upload-Button im cBots-Verzeichnis.
4. **Alten Bot stoppen**, offene Demo-Positionen überprüfen und bei Bedarf separat schließen. Der alte Bot wurde nicht überschrieben.
5. Neuen Bot `MariusAdaptiveDemo` mit nur **einer** Cloud-Instanz im **Demokonto** starten.
6. Im Journal auf `M15trend`, `LEARNING ... NORMAL`, `LEARNING ... DEFENSIVE`, `SIGNAL`, `MIN LOT NOT AFFORDABLE`, `DEMO ORDER OPENED` achten.
7. Den Bot mindestens mehrere Wochen vor einem Urteil testen. Erst nach 12 **geschlossenen** Positionen eines Markts in derselben Richtung kann eine defensive Anpassung einsetzen.

## Grenzen
- Kein laufender ChatGPT-Liveanschluss, kein automatischer KI-Nachrichtenimport, keine selbsttätige Optimierung von frei wählbaren Hyperparametern.
- Die cTrader Cloud unterstützt keine gewöhnlichen HTTP-Anfragen und löscht eigene lokale Dateien bei Cloud-Neustarts.
- Brokerhistorie und Stopps werden nur für diese Bot-Markierung und das dedizierte Demokonto berechnet. Keine manuellen Orders oder anderen Bots parallel; nicht ohne Kontrolle auf Live-Konto übertragen.
- Tagesgrenzen sind technisch nicht garantiert. Bot kann auch null Trades machen. Monatlich +10 % ist sehr ambitioniert, keinesfalls zugesichert und kann auch mit dieser Strategie deutlich verfehlt werden.

**Offiziell dokumentiert:** https://help.ctrader.com/ctrader-algo/references/Trading/History/History/FindAll/ und https://help.ctrader.com/ctrader-algo/documentation/cbots/cloud-requirements/

## Monatsbilanz und Restart-Sicherheit
Die Monatsanfangs-Balance und UTC-Tagesanfangs-Balance werden aus Kontobalance abzüglich des *geschlossenen Nettogewinns dieses Bots* seit Monats- bzw. Tagesbeginn rekonstruiert. Das funktioniert nur mit vollständiger Brokerhistorie und einem getrennten Demokonto ohne Ein-/Auszahlungen, ohne manuelle Trades und ohne andere Bots. Bei fehlerhafter oder unvollständiger Historie kann die Berechnung falsch sein; überprüfe Journal und Kontoequity. Spreads, Slippage, Kurslücken und Ausfälle können 3 % Verlust überschreiten. In der cTrader-Cloud läuft der Bot nicht uneingeschränkt für immer und muss gegebenenfalls neu gestartet werden.
