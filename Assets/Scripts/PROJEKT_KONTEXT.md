# Slimily – Gameplay-Konzept, Kern-Systeme & Offene Design-Optionen

Das Konzept von **Slimily** vereint die taktische Vorausplanung eines Grid-Deckbuilders mit dem nahtlosen Bewegungsfluss eines präzisen 2D-Platformers[cite: 1, 7]. Es gibt keine künstlichen Runden-Zwangsstopps, keine Ladebildschirme zwischen Räumen und keine erzwungenen Kamerawechsel[cite: 1, 7].

---

## 1. Dual-Layer-Prinzip & 360°-Platforming (Mikro-Ebene)

* **Einheitliche Perspektive (Seitenansicht):** Makro-Übersichtskarte (Grid) und Mikro-Platformer-Räume teilen sich dieselbe orthogonale 2D-Ausrichtung[cite: 1, 7]. Eine Kachel im Norden auf dem Grid entspricht exakt dem oberen Ausgang an der Raumdecke[cite: 1, 7].
* **Nahtloses 360°-Klettern & Ecken-Kontrolle:** Der Slime haftet an Böden, Wänden und Decken, rollt kontrolliert um $90^\circ$-Innenecken sowie $270^\circ$-Außenecken und kann direkt auf scharfen Kanten im $45^\circ$-Winkel anhalten oder diagonal von ihnen abspringen[cite: 1, 7].
* **Volles Movement-Repertoire (Bereits umgesetzt):** Neben dem 360°-Oberflächen-Klettern und variablen Sprüngen (inkl. Coyote Time, Jump Buffering und Apex Control) verfügt der Slime über **Run (Sprint)**, **Dash**, **Double Jump (Doppelsprung)** und **Float (Schweben/Gleiten)**[cite: 1, 7].
* **Räume als 360°-Parcours:** Da alle Flächen und die Luft durch Dash, Double Jump und Float voll nutzbar sind, fungieren auch Decken und Wände als vollwertige Hindernis-Routen (mit Non-Stick-Oberflächen, Stacheln und Engstellen)[cite: 1, 7].
* **Waffenloses Gameplay:** Der Slime besitzt keine direkten Waffen oder Angriffswerte[cite: 1, 7]. Gefahren und Bosse werden durch präzises Movement, Raum-Mechanismen, Überlebensphasen und geschicktes Bauen auf dem Grid überwunden[cite: 1, 7].

---

## 2. Zeit- & Bedrohungssystem (Aktionssymbole oben rechts)

Statt eines „Zug beenden“-Buttons oder einer starren Aktionsleiste reagiert die Welt im Takt der Spieleraktionen über **dynamische Countdown-Symbole am oberen rechten Bildschirmrand**[cite: 7].

### 2.1 Was lässt die Zähler herunterticken?
* **0 Zähler (Kostenlos):** Freies Bewegen, Klettern, Dashen, Springen und Schweben im aktuellen Raum sowie das Durchqueren bereits besuchter/gesäuberter Räume[cite: 1, 7].
* **1 Zähler (Standard):** Einen neuen, noch ungesäuberten Raum erstmals betreten oder einen regulären 6er-Würfel auf das Grid bauen[cite: 1, 7].
* **0 bis 2 Zähler (Kartenabhängig):** Spezielle Bau-Formen oder Fähigkeiten besitzen individuelle Tick-Kosten[cite: 7].
* **1 Straf-Zähler (+ Albtraum-Anstieg):** Im Platformer von einem Hindernis oder Gegner getroffen werden (`TakeDamage`)[cite: 1, 7].
* **2 Zähler (Hand erneuern / Mulligan):** Per Tastendruck (`E` / `R3`) die aktuelle Hand abwerfen und sofort 5 neue Formen ziehen[cite: 7].

### 2.2 Aufbau & Verhalten der Aktionssymbole
* **Dynamische Anzahl (0 bis X):** Oben rechts werden nur dann Medaillons eingeblendet, wenn tatsächlich eine Aktion von Boss, Minion oder Umwelt angekündigt ist[cite: 7]. Plant ein Boss zwei unterschiedliche Aktionen gleichzeitig, erscheinen zwei separate Symbole mit eigenem Countdown.
* **Visuelle Kodierung (Wer, Was, Wann, Wo):**
  * **Rahmenform = WER:** Gezacktes Wappen (Boss), runder Steinring (Minion/Scherge), Raute (globaler Umwelteffekt).
  * **Innensymbol = WAS:** Zeigt den Effekt (z. B. Krater = Raum zerstören[cite: 1, 7], Ketten = Türen sperren[cite: 1, 7], Dornen/Gift = Raumgefahr[cite: 1, 7]).
  * **Zahl = WANN:** Verbleibende Ticks bis zur Auslösung[cite: 7].
  * **Farbe = WO:** Entspricht 1:1 der Farbmarkierung der betroffenen Zielkacheln auf dem Makro-Grid[cite: 7].
* **R2-Inspektor & Live-Kamera-Ausschnitt (Picture-in-Picture):**
  * Mit `R2` (oder `Tab`) kann der Spieler jederzeit durch die Symbole oben rechts schalten, um Details und den Zielsektor im Live-Kamera-Ausschnitt zu prüfen[cite: 7].
  * Erreicht ein Zähler `0`, wird die Aktion nahtlos im Hintergrund ausgeführt[cite: 1, 7]. Gleichzeitig öffnet sich oben rechts unter den Symbolen automatisch das kleine **Live-PiP-Fenster**, zeigt für ca. 2,5 Sekunden live den Einschlag/Zug auf dem Grid und blendet sich danach über 1,5 Sekunden sanft wieder aus (`Fade-Out`), während der Spieler im Raum unterbrechungsfrei weiterläuft[cite: 7].

---

## 3. Biom-Resonanz, Dunkles Grid & Albtraumleiste (Oben links)

### 3.1 Ruhiges, dunkles Grid (Keine festen Farb-Sektoren im Nebel)
* Das unbebaute Makro-Grid ist im Nebel (`CellState.Empty`) komplett einheitlich dunkelgrau gehalten, um visuelle Reizüberflutung zu vermeiden[cite: 7].
* Nur der Start-Hub im Zentrum und der gefährliche lila/schwarze Außenrand (`PurpleNightmare` / `BlackVoid`) sind vorab auf dem Raster markiert[cite: 7].

### 3.2 Die Biom-Resonanz (Oben links)
* Oben links sieht der Spieler die aktuelle **Biom-Resonanz** in Prozent (Start im Hub z. B.: `Wald 80% | Ruinen 10% | Meer 10%`) sowie in eckigen Klammern die Anzahl der bereits auf dem Grid gebauten Kacheln jedes Bioms (`[Gebaut-Zähler]`)[cite: 7].
* **Dynamische Verschiebung durch Laufrichtung:** Verlässt der Slime im Platformer einen Raum durch eine Tür (Nord, Ost, Süd, West), verschiebt sich die Resonanz-Anzeige oben links automatisch um einen einstellbaren Prozentsatz zugunsten des Bioms dieser Himmelsrichtung (z. B. Norden stärkt *Ruinen*, Osten stärkt *Meer*, Süden/Westen stärkt *Wald*)[cite: 7].
* **Modulare Schnittstelle:** Über dieselbe Schnittstelle (`ShiftResonance`) können auch Bosse, Schreine oder Events die Biom-Wahrscheinlichkeiten manipulieren[cite: 7].

### 3.3 Die globale Albtraumleiste (0 % – 100 %)
* Sitzt direkt unter der Biom-Resonanz und ersetzt klassische Lebenspunkte[cite: 1, 7].
* Steigt bei Treffern im Raum, bei ausgelösten Gegner-Symbolen, bei Raumzerstörung unter dem Slime sowie beim Aufenthalt auf lila/schwarzen Randfeldern[cite: 1, 7].
* Sinkt durch Schreine und erfolgreiche Konter[cite: 1, 7]. Bei 100 % scheitert der Run und der Slime erwacht wieder im Hub[cite: 1, 7].

---

## 4. Hand-Formen (6er-Würfel), Farb-Snapshot & Bausystem (Unten)

* **Reine Geometrie im Deck (`CardAsset`):** Karten werden modular als ScriptableObjects (oder über die Inspector-Liste im `InventoryManager`) verwaltet und definieren zunächst nur die 6er-Würfelnetz-Form (Hexomino), die Tick-Kosten und optionale Modifikatoren[cite: 7].
* **Farb-Snapshot beim Ziehen:** In dem Moment, in dem eine Form vom Nachziehstapel auf die Hand gezogen wird, würfelt jeder ihrer 6 Blöcke anhand der aktuellen **Biom-Resonanz** (oben links) sein Biom aus[cite: 7].
* **Volle Planbarkeit in der Hand:** Die ausgewürfelten Biom-Farben der 6 Blöcke sind sofort unten in der Hand-Vorschau sichtbar[cite: 7]. Dreht der Spieler die Form im Baumodus (`L1`/`R1` oder `Q`/`E`), wandern die Farben exakt mit[cite: 7]. Beim Platzieren auf dem Grid überträgt jeder Block seine Farbe und sein Biom dauerhaft auf die Grid-Kachel[cite: 7].
* **Nachschub durch Raum-Fortschritt (Clear-Draw):** Immer wenn der Slime im Platformer einen **neuen, ungesäuberten Raum** betritt, zieht er automatisch `+1` neue Form (mit den neuen Resonanz-Prozenten!) auf die Hand[cite: 7].
* **Tether-Regel (Bau-Reichweite):** Ein neuer 6er-Würfel muss an mindestens ein bereits gebautes Feld angrenzen **und** mindestens einer seiner 6 Blöcke muss im Umkreis von maximal 2 Feldern um die aktuelle Slime-Position liegen[cite: 1, 7].
* **Modulare Test-Schalter:** Im Inspector des `InventoryManager` können Fähigkeiten-Karten (`includeSkillCards`) und Block-Modifikatoren (`includeBlockModifiers` für Feuer, Eis, Gift, Schrein) jederzeit per Haken ein- oder ausgeschaltet werden, ohne Code zu löschen[cite: 7].

---

## 5. Offene Design-Optionen & Nächste Ausbaustufen

Die folgenden Punkte sind architektonisch bereits vorbereitet und können je nach Spielgefühl beim Testen flexibel entschieden oder kombiniert werden:

### Option A: Hand-Management & Fähigkeiten-Aufbewahrung
1. **Klassisches Handlimit (Aktuell aktiv):** Bei 5 Karten auf der Hand stoppt das Nachziehen, bis wieder etwas gebaut oder per Mulligan (`E`/`R3`) abgeworfen wird[cite: 7].
2. **Fließband-Hand (Conveyor-Belt / FIFO):** Betritt der Spieler bei voller Hand einen neuen Raum, schiebt sich die neue Form links rein und drückt automatisch die älteste Form rechts in den Ablagestapel. *Vorteil:* Verhindert das endlose Aufsparen von einfachen Start-Würfeln aus dem Hub, ohne die Farben bestehender Handkarten unvorhersehbar zu ändern.
3. **Spezial-Fähigkeiten sichern:**
   * *Variante 1 (Pin-Funktion):* Der Spieler kann genau 1 Element auf seiner Hand per Taste „anpinnen“, damit es nicht vom Fließband oder Mulligan abgeworfen wird.
   * *Variante 2 (Getrennte Slots):* 4 Slots unten für reine 6er-Bauformen + 1–2 separate Fassungen daneben speziell für gezogene Fähigkeiten/Impulse.

### Option B: Biom-Nachbarschafts-Effekte (Farb-Puzzeln auf dem Grid)
Da Würfel oft gemischte Biom-Farben besitzen und drehbar sind[cite: 7], lassen sich beim Anlegen belohnende Puzzel-Regeln aktivieren:
1. **Biom-Harmonie:** Dockt ein neuer Block an ein bereits liegendes Feld derselben Biom-Farbe an, gibt es einen kleinen Bonus (z. B. `-1 %` bis `-2 %` Albtraum).
2. **Cluster-Meilensteine:** Bilden z. B. 8 zusammenhängende Felder derselben Farbe ein geschlossenes Gebiet, spawnt im letzten Raum automatisch ein Schrein oder ein positiver Raum-Effekt.
3. **Biom-Kombinationen:** Bestimmte Farbgrenzen erzeugen Spezialeffekte (z. B. *Wald + Meer* = Heilquelle; *Ruinen + Albtraum* = schwerer Herausforderungsraum mit Trinket-Belohnung).

### Option C: Boss-Trigger & Boss-Mechaniken über Biom-Zähler
Weil Bosse durch das dynamische Biom-System nicht mehr an einen festen 120°-Karten-Sektor gefesselt sind[cite: 7], ergeben sich neue Möglichkeiten:
1. **Erwachen & Phasenwechsel über `[Gebaut-Zähler]`:** Ein Boss spawnt oder wechselt seine Phase erst, wenn eine bestimmte Anzahl eines Bioms auf dem Grid liegt (z. B. *„Bei 20 gebauten Wald-Feldern erwacht der Jäger“*)[cite: 7].
2. **Biom-Konter gegen Bosse:** Ein Ruinen-Festungsboss ist gepanzert, solange Ruinen dominieren – der Spieler muss gezielt in Richtung Wald-Resonanz laufen und X Wald-Felder an den Boss heranbauen, um seine Rüstung zu überwuchern.
3. **Resonanz-Vergiftung durch Boss-Angriffe:** Statt nur Räume zu löschen, kann ein Boss-Angriff `30 % Sumpf/Albtraum` in die Resonanz oben links mischen, sodass neu gezogene Würfel gefährliche Kacheln enthalten, bis der Spieler einen Reinigungs-Schrein erreicht.

### Option D: Aktives Entschärfen im Raum & Passive Trinkets
1. **Epizentrum-Konter (`CancelCast`):** Größere Boss-Gefahrenzonen auf dem Grid erhalten 1 markiertes Fadenkreuz-Feld[cite: 1, 7]. Erreicht der Slime diesen Raum vor Ablauf des Symbol-Countdowns und aktiviert den Unterbrecher im Platformer-Parcours, bricht der Boss-Angriff ab[cite: 1, 7].
2. **Trinkets (Relikte):** Dauerhafte passive Upgrades, die das erweiterte Movement (Dash, Double Jump, Float), die Biom-Resonanz und die Countdown-Symbole verknüpfen (z. B. *„Beim Mischen des Ablagestapels erhöht sich der Countdown aller Gefahren-Symbole um +1“*).