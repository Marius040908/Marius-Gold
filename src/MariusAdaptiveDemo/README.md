# Marius Adaptive DEMO v2 – Gold, Silber, Bitcoin, Ethereum

**Experimenteller cTrader-C#-Demobot – kein Profitversprechen, keine selbstlernende neuronale KI.** Neue Version, getrennt vom bestehenden MariusMultiAssetDemo. Keine Echtgeld-Orders im Code (Account.IsLive stoppt den Bot). Die einzige Startinstanz lädt alle vier Märkte selbst.

## Analyse
- **M15 (15 Minuten)**: abgeschlossene Kerze und EMA20/EMA50 bestimmen die übergeordnete Trendrichtung und helfen beim dynamischen Ausstieg.
- **M5 (5 Minuten)**: EMA9/EMA21 und RSI14 bestimmen eine mögliche Long- oder Short-Einstiegsgelegenheit; ATR bestimmt den Stop-Loss.
- **Keine Begrenzung der Trade-Anzahl pro Tag. Bis zu vier gleichzeitige Positionen**, maximal eine je Gold, Silber, Bitcoin und Ethereum. Vier Positionen sind eine Obergrenze, kein verpflichtendes Handelsvolumen.
- Positionen nur, wenn Mindestlot, geschätztes Verlustbudget, Margin, Spread, Handelszeit und aktuelle Kontoexposition passen.
- **Neuer Plan: Monatsgewinnziel +10 %**, maximal **1 % Tagesverlust** vom rekonstruierten UTC-Tagesstartkapital (bei 2.000 EUR etwa 20 EUR). Das 10-%-Tagesziel ist vollständig entfernt.
- **Kein tägliches Gewinnlimit**: Der Bot berechnet die rechnerisch nötige Tagesrendite für +10 % bis zum Monatsende (etwa +0,318 % bei 30 Tagen) **nur als Orientierung im Journal**. Er handelt bei guten Signalen weiter, auch wenn dieser Richtwert überschritten ist. Sobald das Monatsziel erreicht wird, versucht er seine Positionen zu schließen und bis zum nächsten UTC-Monat zu pausieren.
- **Keine Verlustjagd**: Wenn der Bot hinter seinem Monatsziel liegt, erhöht er nicht das Risiko oder den Hebel. Neue Positionen dürfen höchstens **0,20 %** des aktuellen Kontoeigenkapitals als geschätztes SL-Risiko tragen (bei 2.000 EUR etwa 4 EUR); alle offenen Bot-Positionen zusammen maximal **0,80 % des rekonstruierten Tagesstartkapitals** (bei 2.000 EUR etwa 16 EUR) als geschätztes SL-Risiko. Der Rest dient als Puffer, ist aber kein garantierter Schutz gegen Slippage/Gebühren. Mindestlot, Margin, Signal und verfügbare Risikobudgets können weniger als vier Positionen erlauben.

## Dynamische Gewinnmitnahme für 15 / 25 / 40 EUR und mehr
- **Kein festes Take-Profit-Ziel** mehr. Ein neuer Trade startet mit ATR-basiertem Stop-Loss, aber ohne starre 1,5-R-Auszahlung; seine Gewinnchance ist nach oben nicht durch ein TP-Level begrenzt.
- **15, 25 und 40 Einheiten der Kontowährung** (bei EUR-Konto entsprechend Euro) werden im Journal als erreichte **schwebende Nettogewinn-Meilensteine** protokolliert, aber **nicht als Pflichtausstieg**. Auch Beträge darüber sind möglich, nicht garantiert.
- **Ab einem Kursgewinn von ca. 1,1 ATR** (14-Kerzen ATR auf M5) aktiviert die Logik Gewinnschutz: Der Stop-Loss wird mindestens geringfügig auf die Gewinnseite des Einstiegspreises gezogen (ohne Garantie auf Netto-Break-even nach Ausführungskosten).
- **Dynamischer ATR-Trailing-Stop**: Bestätigt M15 den Trend, bleibt der Stop weiter entfernt, sodass Gewinner laufen können. Ist der Trend schwächer, wird enger nachgezogen. Bei einer bestätigten M15-Trendumkehr versucht der Bot die Position zu schließen.
- **Keine Risikoerhöhung für große Gewinnziele**: Erzwungene Gewinne von 15, 25 oder 40 EUR wären mit 1 % Tagesverlustbudget und vier möglichen Positionen nicht seriös garantiert erreichbar. Nicht jeder Tag bietet passende Signale, und eine Mindestlotprüfung darf das Öffnen verhindern.
- **Wichtig**: Das dynamische Nachziehen und die Trendumkehr-Ausgänge funktionieren nur, solange die Bot-Instanz läuft. Der jeweils zuletzt vom Broker akzeptierte Stop-Loss bleibt als Sicherheitsorder bestehen. Kurslücken, Slippage und technische Probleme können trotzdem höhere Verluste verursachen.

## Lernmechanismus nach mehreren Fehlern
- Bewertet für **jeden Markt und jede Richtung getrennt** die letzten **12 abgeschlossenen Positionen** dieses Bots aus dem cTrader-Handelsverlauf.
- Nur wenn **mindestens 8 der letzten 12 Trades verloren** haben UND **die Summe ihrer Nettogewinne negativ** ist, wird der Modus DEFENSIVE aktiviert.
- In DEFENSIVE werden RSI- und Momentum-Anforderungen strenger; das geschätzte Risikobudget neuer Orders wird **halbiert**. Erhöht Risiko niemals als Reaktion auf Gewinne oder Verluste.
- Mit neuen abgeschlossenen Trades wird das rollierende Fenster erneut ausgewertet; wird die Verlustbedingung nicht mehr erfüllt, kehrt die Strategie zu NORMAL zurück.
- Kein Reagieren auf einzelne Fehler, kein Nachkaufen von Verlusten, kein Martingale, kein Training eines neuronalen Netzes.
- Ergebnisse aus *früheren Bot-Versionen* fließen bewusst nicht ein, weil deren Strategie anders war. Es müssen zuerst genügend Trades dieser Version stattfinden.
- Die Lernbasis liegt in der **cTrader-Handelshistorie**, nicht im Cloud-Dateispeicher. Nach Neustart wird aus dem Brokerverlauf rekonstruiert, soweit die Historie vollständig verfügbar ist.
- Learning-Journal-Meldungen zeigen Samplezahl, Nettoergebnis, Verlustanzahl und NORMAL/DEFENSIVE, damit die Anpassung überprüfbar ist.

## Verhalten am Monatsende
- Gewinnziel: +10 % des anhand der Bot-Historie rekonstruierten Monatsstartkapitals. Erreicht die Equity das Monatsziel, hält der Bot die restlichen Tage dieses Monats an (soweit die Cloud-Instanz läuft).
- Nach einem normalen Cloud-Neustart versucht er eine zuvor erreichte **realisierte** Monatszielmarke aus der Handelshistorie zu rekonstruieren. War die +10-%-Schwelle nur kurz in schwebenden Gewinnen erreicht und endete das Schließen darunter, kann der Monatsstopp nach einem Neustart nicht zuverlässig rekonstruiert werden. Kontrolliere deshalb die Demo-Statistiken; kein garantiert durchgängiger Schutz.
- Zum UTC-Monatswechsel wird die Monatsziel-Sperre zurückgesetzt, während ein Tagesverluststopp jeden UTC-Tag neu bewertet wird.

## Installation auf dem iPad
1. GitHub Actions "Build Marius Adaptive DEMO v2" abwarten (grüner Status).
2. Aus dem Artefakt `MariusAdaptiveDemo-v2` die `.algo`-Datei herunterladen.
3. In der iOS Dateien-App die `.algo`-Datei öffnen und in Pepperstone cTrader importieren. Alternativ über den Upload-Button im cBots-Verzeichnis.
4. **Alten Bot stoppen**, offene Demo-Positionen überprüfen und bei Bedarf separat schließen. Der alte Bot wurde nicht überschrieben.
5. Neuen Bot `MariusAdaptiveDemo` mit nur **einer** Cloud-Instanz im **Demokonto** starten.
6. Im Journal auf `M15trend`, `LEARNING ... NORMAL`, `LEARNING ... DEFENSIVE`, `SIGNAL`, `MIN LOT NOT AFFORDABLE`, `DEMO ORDER OPENED` achten.
7. Die neue Version nach erfolgreichem Build **neu importieren**, den alten Demobot vorher stoppen und offene Positionen kontrollieren. Prüfe im neuen Journal `DYNAMIC EXIT`, `NO FIXED TAKE PROFIT`, `DAILY LOSS STOP -1%`, `UP TO FOUR` und `MONTH PLAN`. Den Bot mindestens mehrere Wochen vor einem Urteil testen. Erst nach 12 **geschlossenen** Positionen eines Markts in derselben Richtung kann eine defensive Anpassung einsetzen.

## Grenzen
- Kein laufender ChatGPT-Liveanschluss, kein automatischer KI-Nachrichtenimport, keine selbsttätige Optimierung von frei wählbaren Hyperparametern.
- Die cTrader Cloud unterstützt keine gewöhnlichen HTTP-Anfragen und löscht eigene lokale Dateien bei Cloud-Neustarts.
- Brokerhistorie und Stopps werden nur für diese Bot-Markierung und das dedizierte Demokonto berechnet. Keine manuellen Orders oder anderen Bots parallel; nicht ohne Kontrolle auf Live-Konto übertragen.
- Tagesgrenzen sind technisch nicht garantiert. Bot kann auch null Trades machen. Monatlich +10 % ist sehr ambitioniert, keinesfalls zugesichert und kann auch mit dieser Strategie deutlich verfehlt werden.

**Offiziell dokumentiert:** https://help.ctrader.com/ctrader-algo/references/Trading/History/History/FindAll/ und https://help.ctrader.com/ctrader-algo/documentation/cbots/cloud-requirements/

## Monatsbilanz und Restart-Sicherheit
Die Monatsanfangs-Balance und UTC-Tagesanfangs-Balance werden aus Kontobalance abzüglich des *geschlossenen Nettogewinns dieses Bots* seit Monats- bzw. Tagesbeginn rekonstruiert. Das funktioniert nur mit vollständiger Brokerhistorie und einem getrennten Demokonto ohne Ein-/Auszahlungen, ohne manuelle Trades und ohne andere Bots. Bei fehlerhafter oder unvollständiger Historie kann die Berechnung falsch sein; überprüfe Journal und Kontoequity. Spreads, Slippage, Kurslücken und Ausfälle können die 1-%-Tagesverlustgrenze überschreiten. In der cTrader-Cloud läuft der Bot nicht uneingeschränkt für immer und muss gegebenenfalls neu gestartet werden.
