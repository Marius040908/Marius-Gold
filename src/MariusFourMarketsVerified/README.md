# Marius Four Markets Verified – separate DEMO cBot

**Wichtig**: „Verified“ bedeutet nur durch Code-/Regelprüfungen und GitHub-Build überprüft, **nicht** geprüfte Rentabilität. Kein historischer Kurs-Backtest ausgeführt. Live-Konten sind durch `Account.IsLive` gesperrt.

## Vorgaben
- Automatisch Gold XAUUSD, Silber XAGUSD, Bitcoin BTCUSD und Ethereum ETHUSD.
- Maximal **2 zeitgleich offene Positionen**, maximal eine je Instrument; **keine feste Anzahl Trades pro Tag**, aber **keine Trades ohne sämtliche Eintrittsfilter**.
- **Maximal 1 % der Kontoequity** als geplantes Verlustbudget für jede neue Position; bei 2.000 € höchstens etwa 20 €. Broker-Mindestlot wird niemals zur Risikoerhöhung aufgerundet. Je nach Profil/Signalstärke sind niedrigere Risiken üblich; nach 8 von 12 Verlusten und negativer Summe wird Budget halbiert.
- **Take-Profit-Ziel 3 % der Kontoequity beim Öffnen der Position**: bei 2.000 € etwa +60 €, unabhängig von tatsächlich verwendetem Risikobudget. TP-Pips werden aus geplanter Account-Währung und real abgerundeter Lotgröße geschätzt. Bei geringerem Risiko liegt das TP relativ weiter weg und kann praktisch schwerer erreichbar sein.
- Brokerseitiger Stop-Loss und Take-Profit werden **beim Orderversand** gesetzt. Es gibt keine vorzeitige Trend-Exit-Logik (außer Sitzungsende) und keinen dynamischen Trailing-Stop, damit 3-%-TP nicht durch freiwillige frühe Ausstiege gekappt wird. Außerhalb 07:00–21:00 UTC versucht der Bot im Sinne von Daytrading Positionen zu schließen. Somit sind 3 % keine Mindest- oder garantierte Trade-Rendite.
- Keine Tages- oder Monatsgewinn-/Verluststopps. Mehrere Verlusttrades können sich kumulieren, zwei Positionen können zusammen bis 2 % geplantes Anfangsrisiko bedeuten. Slippage/Gaps können mehr Verlust verursachen.
- Handelsgebühren, Umrechnungen, Swaps und Ausführungskurse verändern Nettogewinn. Der +3%-TP basiert auf geschätztem, nicht garantiertem Preis/Volumen. Nach Ausführung prüfen.

## Marktspezifische Signal-Filter (experimentell)
Gemeinsam: abgeschlossene **M5**-Kerze EMA9/EMA21 und RSI14 bestätigt Bewegung; abgeschlossene **M15** EMA20/EMA50 Richtung und Trendstärke; **H1** EMA20/EMA50 Richtung bestätigt. Zusätzlich:
- Gold: Spread <=12 % M5-ATR, M15-EMA-Abstand >=0,13 M15-ATR, Kerzenkörper >=0,09 M5-ATR, RSI Long 55–70 / Short 30–45.
- Silber: Spread <=10 % ATR, M15-Abstand >=0,20 ATR, Kerzenkörper >=0,12 ATR, RSI Long 57–69 / Short 31–43.
- Bitcoin: Spread <=15 % ATR, M15-Abstand >=0,16 ATR, Kerzenkörper >=0,13 ATR, RSI Long 56–70 / Short 30–44.
- Ethereum: Spread <=14 % ATR, M15-Abstand >=0,19 ATR, Kerzenkörper >=0,15 ATR, RSI Long 58–70 / Short 30–42.

Diese Schwellen sind **plausible Entwurfswerte**, keine statistisch mit unabhängigen Pepperstone-Kursdaten optimierten Parameter. Sie können **zu null Trades** führen; kein erzwungenes Trading oder Signal-Lockern für Gewinne.

## Lernregel
Liest den Brokerverlauf abgeschlossener Trades mit eigenem eindeutigen Bot-Label, gruppiert ggf. Teilabschlüsse je Positions-ID, getrennt nach Markt und Kauf/Verkauf. Erst ab 12 Positionen derselben Richtung und desselben Instruments: bei mindestens 8 Verlusten und negativem kumuliertem Nettoergebnis Wechsel in DEFENSIVE (strengere RSI-Anforderungen und halbiertes neues Risikobudget). Nach Änderung der gleitenden 12er-Serie Rückkehr zu NORMAL. **Statistische Anpassung, kein maschinelles Selbsttraining.** Die vorherigen Bot-Labels werden nicht mitgelernt.

## Nachweis und Tests
GitHub Actions führt explizite deterministische Tests für das 1%-/3%-Verhältnis bei unterschiedlichen realen Positionsrisiken, Risikocaps, alle vier Marktprofile, schlechte Spreads, schwache Trends und Lernschwelle aus. Danach kompiliert es den Bot und stellt die `.algo`-Datei bereit.
**Diese Tests sind kein Backtest**. Ein aussagekräftiger Backtest muss anschließend mit historischen Pepperstone-Daten, Gebühren und Slippage auf einem geeigneten cTrader-System stattfinden; danach mehrwöchiger Forward-Demotest. Noch kein Nachweis über Trefferquote, Profitabilität oder Prognosegüte.

## Installation / Datensammlung in Pepperstone-cTrader-App
- Nur **eine laufende Bot-Instanz** auf einem dedizierten Demokonto verwenden. Vor Wechsel alte Bot-Instanz stoppen, ihre offenen Positionen prüfen. Der andere Quellcode bleibt erhalten.
- In die cTrader-App die .algo-Datei importieren, Cloud-Instanz auf Demo starten; für die ersten Minuten Journal lesen.
- Für Analyse Screenshots von „Positionen → Verlauf“, einzelnen Trade-Details (SL, TP, Zeit, Gebühren) und „cBot → Journal“ senden. Screenshot allein genügt nicht für statistische Optimierung; Export mit Entry/Exit/Label/Zeit ist später sinnvoll.
- Keine Kontopasswörter, Tokens oder API-Schlüssel weitergeben.
