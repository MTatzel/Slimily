# Slimily – Spieldesign-Konzept, Kern-Systeme & Entwicklungsstand

Das Konzept von **Slimily** vereint die räumliche Taktik eines Grid-Builders mit dem nahtlosen Bewegungsfluss eines präzisen 2D-Platformers[cite: 1, 4]. Es gibt keine künstlichen Runden-Zwangsstopps, kein klassisches Kartendeck-Menü, keine Ladebildschirme zwischen Räumen und keine erzwungenen Kamerawechsel[cite: 1, 4].

---

## 1. Dual-Layer-Prinzip & 360°-Platforming (Mikro-Ebene)

* **Einheitliche Perspektive (Seitenansicht):** Makro-Übersichtskarte (Grid) und Mikro-Platformer-Räume nutzen dieselbe orthogonale 2D-Ausrichtung[cite: 1, 4]. Eine Kachel im Norden auf dem Grid entspricht exakt dem oberen Ausgang an der Raumdecke[cite: 1, 4].
* **Nahtloses 360°-Klettern & Ecken-Kontrolle:** Der Slime haftet an Böden, Wänden und Decken, rollt kontrolliert um $90^\circ$-Innenecken sowie $270^\circ$-Außenecken und kann direkt auf scharfen Kanten im $45^\circ$-Winkel anhalten oder diagonal von ihnen abspringen[cite: 1, 4].
* **Volles Movement-Repertoire:** Neben dem 360°-Oberflächen-Klettern und variablen Sprüngen (inkl. Coyote Time, Jump Buffering und Apex Control) verfügt der Slime über **Run (Sprint)**, **Dash**, **Double Jump (Doppelsprung)** und **Float (Schweben/Gleiten)**[cite: 1, 4].
* **Räume als 360°-Parcours:** Da alle Flächen und der Luftraum durch Dash, Double Jump und Float voll nutzbar sind, fungieren auch Decken und Wände als vollwertige Hindernis-Routen (mit Non-Stick-Oberflächen, Stacheln, Geheimwänden und Engstellen)[cite: 1, 4].
* **Waffenloses Gameplay:** Der Slime besitzt keine direkten Waffen oder Angriffswerte[cite: 1, 4]. Gefahren, Beute-Gegner und Bosse werden ausschließlich durch präzises Movement, Raum-Mechanismen, Überlebensphasen und kluges Bauen auf dem Grid überwunden[cite: 1, 4].

---

## 2. Das Tick- & Bedrohungssystem (Aktionssymbole oben rechts)

Statt eines „Zug beenden“-Buttons reagiert die Welt im Takt deiner Aktionen über **dynamische Countdown-Symbole am oberen rechten Bildschirmrand**[cite: 4].

### 2.1 Die einheitliche Tick-Regel (Ohne „Besucht/Unbesucht“-Ballast)
Um das Makro-Grid visuell sauber zu halten und jederzeit volle taktische Spannung (z. B. beim Entkommen aus einer $3\times 3$-Boss-Gefahrenzone) zu garantieren, gibt es keine Unterscheidung mehr zwischen „besuchten“ und „unbesuchten“ Räumen:
* **0 Ticks (Kostenlos):** Freies Klettern, Dashen, Springen, Schweben und Looten innerhalb des aktuellen Raums[cite: 1, 4].
* **1 Tick (Raumwechsel):** Jedes Durchschreiten einer Tür in einen Nachbarraum (egal ob zum ersten oder zweiten Mal).
* **1 Tick (Standard-Bauen):** Einen 6er-Würfel aus dem Inventar auf das Makro-Grid setzen (spezielle Risiko- oder Fluch-Würfel können 0 bis 2 Ticks kosten)[cite: 1, 4].
* **1 Tick (Schrein-Teleport):** Lokaler Sprung von einem aktiven Schrein zu einem anderen Schrein in Reichweite.
* **1 Straf-Tick (+ Albtraum-Anstieg):** Im Platformer von einem Hindernis oder Gegner getroffen werden (`TakeDamage`)[cite: 1, 4].

> **Taktisches Beispiel ($3\times 3$-Boss-Zone):** Markiert der Boss ein $3\times 3$-Gebiet um den Slime mit einem Countdown von `4`, weiß der Spieler sofort: *„Ich brauche 2 Raumwechsel (`2 Ticks`), um den Rand zu erreichen – mir bleiben also genau `2 Ticks` Puffer, um noch einen rettenden Würfel anzubauen oder mir maximal einen Fahrfehler im Raum zu erlauben.“*

### 2.2 Aufbau & Verhalten der Aktionssymbole
* **Dynamische Anzahl (0 bis X):** Oben rechts werden nur dann Medaillons eingeblendet, wenn tatsächlich eine Aktion von Boss, Minion oder Umwelt angekündigt ist[cite: 4].
* **Visuelle Kodierung (Wer, Was, Wann, Wo):**
  * **Rahmenform = WER:** Gezacktes Wappen (Boss), runder Steinring (Minion/Scherge), Raute (Umwelteffekt)[cite: 4].
  * **Innensymbol = WAS:** Zeigt den Effekt (z. B. Krater = Raum zerstören[cite: 1, 4], Ketten = Türen verriegeln[cite: 1, 4], Verschiebungs-Pfeile, Schrein-Korrumpierung).
  * **Zahl = WANN:** Verbleibende Ticks bis zur Auslösung[cite: 4].
  * **Farbe = WO:** Entspricht 1:1 der Farbmarkierung der Zielkacheln auf dem Makro-Grid[cite: 4].
* **R2-Inspektor & Live-Kamera-Ausschnitt (Picture-in-Picture):**
  * Mit `R2` (oder `Tab`) lassen sich die Symbole oben rechts durchschalten, um den Zielbereich im kleinen Kamera-Fenster zu inspizieren[cite: 1, 4].
  * Fällt ein Zähler auf `0`, wird die Aktion nahtlos im Hintergrund ausgeführt[cite: 1, 4]. Gleichzeitig blendet sich oben rechts unter den Symbolen das **Live-PiP-Fenster** ein, zeigt für **2,5 Sekunden** live die Boss-Aktion auf dem Grid und blendet sich danach über **1,5 Sekunden sanft wieder aus (`Fade-Out`)**, während der Spieler im Raum weiterläuft[cite: 1, 4].

---

## 3. Biom-Resonanz, Dunkles Grid & Albtraumleiste (Oben links)

### 3.1 Ruhiges, dunkles Grid im Nebel
* Unbebaute Felder im Nebel (`CellState.Empty`) sind einheitlich dunkelgrau gehalten[cite: 1, 4].
* Nur der Start-Hub im Zentrum und der gefährliche lila/schwarze Außenrand (`PurpleNightmare` / `BlackVoid`) sind vorab sichtbar[cite: 1, 4].
* Jede Kachelfarbe steht direkt für einen eigenen **Biom-Raumpool** (🌲 **Wald** = Grün, 🏛️ **Ruinen** = Gold-Ocker, 🌊 **Meer** = Blau, 🧪 **Sumpf** = Gift-Türkis, 🟣 **Albtraum** = Violett)[cite: 1]. Zusätzliche Element-Mods (Feuer/Eis) entfallen komplett, da das Biom bereits das Raum-Thema bestimmt.

### 3.2 Biom-Resonanz in festen 20-%-Stufen
* **Saubere Stufen & Ausblendung:** Die Resonanz oben links arbeitet in glatten **20-%-Schritten** (`0 %`, `20 %`, `40 %`, `60 %`, `80 %`, `100 %`). Biome mit `0 %` werden im HUD komplett ausgeblendet.
* **Start im Hub:** Der Run startet im Hub mit `🌲 Wald 100%` (innerhalb eines Sicherheitsradius von 2 Feldern um den Hub bleibt der Wald dominant).
* **Dynamische Trigger:**
  1. **Laufrichtung außerhalb des Hub-Gürtels:** Jeder Raumschritt verschiebt die Resonanz um `+20 %` zugunsten der jeweiligen Himmelsrichtung (z. B. Norden = *Ruinen*, Osten = *Meer*, Westen = *Sumpf*, Süden = *Wald*) und zieht `20 %` vom aktuell stärksten anderen Biom ab.
  2. **Boss-Einfluss:** Immer wenn der Boss oben rechts bei `0` zuschlägt, drückt er `+20 %` seines eigenen Bioms (bzw. *Lila Albtraum*) in die Resonanzleiste.
  3. **Albtraum-Überlauf:** Steigt die Albtraumleiste über `60 %`, mischt sich automatisch `20 % Albtraum` in die Resonanz.
* **`[Gebaut-Zähler]`:** Neben dem Prozentwert wird in eckigen Klammern angezeigt, wie viele Kacheln dieses Bioms bereits auf dem Grid liegen (dient als Trigger für Boss-Phasen und Events)[cite: 1, 4].

### 3.3 Die Albtraumleiste (0 % – 100 %): Traum-Stabilität & Schwierigkeitsgrad
* **Klare Trennung zu den Ticks:** Die Albtraumleiste steigt **nicht** mehr passiv pro Runde, sondern misst deine tatsächliche Traum-Stabilität (Lebensleiste + Fieber-Barometer).
* **Wann steigt sie?** Bei Treffern im Platformer-Raum, bei einem Volltreffer durch eine Boss-Zone auf dem Grid, bei Raumzerstörung unter dem Slime sowie beim Betreten des lila/schwarzen Kartenrands[cite: 1, 4].
* **Was bewirkt sie?** Mit steigender Leiste (z. B. ab `40 %` und `70 %`) werden die Räume feindseliger, die Biom-Resonanz korrumpiert schneller und gefundene Würfel tragen häufiger gefährliche Raum-Mods. Bei **100 %** schreckt der Slime aus dem Traum hoch (Run-Ende / Erwachen im Hub)[cite: 1, 4].

---

## 4. Physisches Würfel-Inventar, Raum-Beute & Situative Mods (Unten)

Das frühere Kartendeck (Nachziehstapel, Ablagestapel, Mulligan) ist komplett durch ein **physisches Würfel-Inventar** ersetzt:

### 4.1 Großes Inventar & Farb-Zuweisung beim Finden
* **Volle Auswahl für situatives Handeln:** Der Spieler startet im Hub mit Startwürfeln und kann im Inventar **5 bis 8 Würfel gleichzeitig** tragen[cite: 1], um je nach Grid-Situation zwischen verschiedenen Formen, Biom-Farben und Risiko-Mods abzuwägen.
* **Farb-Snapshot:** Sobald ein Würfel im Platformer eingesammelt wird, würfelt jeder seiner 6 Blöcke anhand der aktuellen **Biom-Resonanz** (oben links) seine Biom-Farbe aus[cite: 1, 4]. Diese Farben sind unten im Inventar sofort sichtbar und drehen sich beim Rotieren (`L1`/`R1` oder `Q`/`E`) exakt mit[cite: 1, 4].
* **Tether-Regel (Bau-Reichweite):** Ein neuer Würfel muss an ein gebautes Feld angrenzen und mindestens 1 Block muss im Umkreis von maximal 2 Feldern um den Slime liegen (`maxBuildDistanceFromSlime = 2`)[cite: 1, 4].

### 4.2 Das „1 + 1“-Beutesystem pro gelegtem 6er-Würfel (`CubeLootManager`)
Immer wenn ein 6er-Würfel auf das Grid gelegt wird, verteilt der `CubeLootManager` die Beute auf die 6 neuen Grid-Koordinaten:
1. **1 Garantierter Weg-Würfel:** Spawnt in einem der 6 neuen Räume an einem regulären Spawnpunkt (`normalCubeSpawnPoints`).
2. **1 Versteckter Risiko-/Bonus-Würfel:** Spawnt in einem anderen der 6 Räume an einer schwer erreichbaren Stelle (`secretCubeSpawnPoints` – z. B. hinter einer Fake-Wand oder über einen schweren Decken-Parcours mit Dash/Double Jump/Float).
3. **Pool-Sicherheit:** Da der Beute-Status pro Grid-Koordinate (`Vector2Int`) gespeichert wird, funktionieren auch mehrfach verwendete Raum-Prefabs aus dem Biom-Pool jederzeit fehlerfrei.

### 4.3 Situative Raum-Modifikatoren (Statt Element-Chaos)
Auf einem 6er-Würfel tragen maximal 1–2 Blöcke ein einzelnes, klares weißes Symbol für eine besondere Raum-Bedingung:
* 🌑 **Finsternis (Blackout):** Raum wird periodisch dunkel; nur Umrisse, Gefahren und der Slime glimmen.
* ⏳ **Survival-Kammer:** Türen verriegeln sich beim Betreten für kurze Zeit; der Spieler muss im 360°-Parcours überleben, bis sie sich öffnen.
* 👁️ **Wächter-Raum:** Enthält spezielle Beute-/Jäger-Gegner für hohes Risiko & zusätzliche Würfel-Ausbeute.
* ⚡ **Instabil (Kollaps):** Raum bricht beim Verlassen hinter dem Spieler zusammen (Einbahnstraße).
* ⛩️ **Schrein / Altar:** Baubarer Knotenpunkt für Heilung, Loot und lokalen Teleport[cite: 1, 4].

---

## 5. Schrein-Netzwerk (Lokale Teleporter, Cooldowns & Boss-Interaktion)

Schreine befinden sich als Modifikator auf bestimmten Würfel-Blöcken und werden vom Spieler bewusst auf dem Grid platziert[cite: 1]:

* **Spieler-Nutzen:** Das Aktivieren eines Schreins im Raum senkt den Albtraum[cite: 1, 4], spawnt einen frischen Würfel und schaltet den Schrein als Teleporter frei. Danach geht der Schrein für X **Ticks** auf Cooldown (oder verbraucht 1 seiner begrenzten Ladungen).
* **Schutz gegen 50-Felder-Cheese (Lokales Kampfgebiet halten):**
  1. **Begrenzte Sprung-Reichweite:** Ein Schrein kann nur zu anderen Schreinen innerhalb eines festen Radius (z. B. maximal **8 Grid-Felder**) teleportieren. Alte Schreine am anderen Ende der Karte sind außer Reichweite.
  2. **Boss-Bannkreis (Arena-Lock):** Sobald sich der Spieler im Gebiet eines aktiven Bosses befindet, verhindert der Bannkreis jedes Heraus-Teleportieren in entfernte Regionen oder den Hub, bis der Boss besiegt ist. Innerhalb des Boss-Gebiets bleiben lokale Schreine als taktische Ausweich-Anker (z. B. aus $3\times 3$-Zonen) voll nutzbar.
* **Boss-Interaktion:** Unbewachte, aufgeladene Schreine in Boss-Nähe können vom Boss selbst als Teleporter genutzt, ihrer Würfel beraubt oder **korrumpiert** werden (müssen dann im Platformer-Raum erst wieder freigekämpft werden).

---

## 6. Organischer Deadlock-Schutz (Ohne künstliche Kauf-Buttons)

Damit der Spieler niemals handlungsunfähig wird – selbst wenn ihm die Würfel ausgehen oder der Boss durch Verschieben/Zerstören ein Schachbrettmuster erzeugt:

### 6.1 Waffenlose „Würfel-Träger“-Gegner (Gegen Würfel-Mangel)
* In den Räumen tauchen spezielle Wesen auf, die einen Würfel-Kern in sich tragen und rein über Movement besiegt werden (z. B. **Flucht-Schemen**, die man im Lauf mit dem **Dash** durchschlagen muss, oder **Panzer-Wirte**, die man gegen eine Wand ködert).
* **Dynamische Gummiband-Spawnrate:** Hat der Spieler 5–7 Würfel im Inventar, tauchen diese Gegner selten auf. Sinkt der Gesamtvorrat (Inventar + erreichbare Raum-Würfel) jedoch auf `1` oder `0`, steigt die Spawn-Chance im nächsten betretenen Raum auf **100 %**.

### 6.2 Würfel-Kompression auf $1\times 1$ / Kreuz (Gegen Schachbrett-Blockaden)
* Verschiebt oder zerstört der Boss Räume auf dem Grid so ungünstig, dass rund um den Slime nur noch einzelne $1\times 1$-Lücken frei sind (in die kein 6er-Würfel mehr passt), kann der Spieler im Baumodus einen seiner 6er-Würfel **komprimieren / opfern**.
* Statt 6 Räumen erzeugt dieser Würfel ein einzelnes **$1\times 1$-Brückenfeld** (oder ein kleines Plus-Kreuz direkt um die Slime-Position), das die Schachbrett-Lücke flickt und abgeschnittene Raum-Inseln wieder verbindet (spawnt dafür aber keinen neuen Beute-Würfel).

### 6.3 Verfluchte Boss-Würfel als offensiver Boss-Skill
* „Schlechte Würfel“ sind kein Notfall-Trostpflaster, sondern eine **aktive Boss-Attacke**: Der Boss kann dem Spieler einen lila verfluchten, verwinkelten Würfel ins Inventar zwingen, der im Inventar passiv Albtraum erzeugt, bis der Spieler ihn irgendwo auf dem Grid verbaut.

---

## 7. Entwicklungsstand: Was ist umgesetzt vs. Was fehlt noch?

| Bereich / Modul | ✅ Bereits im Code umgesetzt | 🛠️ Noch offen / Nächste Schritte |
| :--- | :--- | :--- |
| **360°-Movement & Physik**<br>`PlatformerSlimeController.cs`<br>`GroundedState.cs` / `AirborneState.cs` | 360°-Wand-/Deckenklettern, $90^\circ$-Innen- & $270^\circ$-Außenecken (inkl. $45^\circ$-Stopp & Eck-Sprung), Coyote Time, Jump Buffer, Apex Control, Squash & Stretch sowie **Dash, Double Jump, Run & Float**[cite: 1, 4]. | Raum-Hindernisse für die neuen situativen Mods (z. B. *Blackout*-Lichtsteuerung, *Survival*-Türsperre). |
| **Biom-Resonanz & HUD (Oben links)**<br>`BiomeResonanceManager.cs`<br>`ThreatOrbsAndPiPHUD.cs` | Feste **20-%-Schritte**, Ausblenden von `0 %`-Biomen, Hub-Schutzradius (`hubSafeRadius = 2`), Richtungs-Shift, Boss-Shift bei Puls (`+20 %`), Albtraum-Überlauf ab `60 %` und `[Gebaut-Zähler]`. | Visuelles Feedback im Raum passend zur Albtraum-Stufe; Biom-spezifische Raum-Pools beim Spawnen in `MacroGridManager` anbinden. |
| **Aktionssymbole & Live-PiP (Oben rechts)**<br>`ThreatTrackerManager.cs`<br>`ThreatOrbsAndPiPHUD.cs` | Dynamische Countdown-Medaillons, `R2`-Inspektion, automatisches **Live-PiP-Kamera-Fenster** bei Countdown `0` inkl. **2,5 s Haltezeit + 1,5 s sanftem Fade-Out**[cite: 1, 4]. | Unterschiedliche Rahmen-Sprites (Boss/Minion/Umwelt) zuweisen; mehrere gleichzeitige Boss-Symbole pro Boss-Archetyp. |
| **Physisches Inventar & Raum-Beute**<br>`InventoryManager.cs`<br>`CubeLootManager.cs`<br>`CubePickup.cs` / `Room.cs` | Kartendeck/Ablagestapel entfernt; reines Inventar (bis 8 Slots); pro gelegtem 6er-Würfel spawnen **1 garantierter + 1 versteckter Würfel** an `Room`-Spawnpunkten (inkl. Gizmos & Live-Switch per Taste `P`). | Höhere Standard-Kapazität (z. B. 6–8 Würfel) im Balancing testen; garantiertes Würfel-Symbol optional auf der Vorschau/Grid-Kachel anzeigen. |
| **Tick-Regel & Albtraum-Trennung**<br>`GameManager.cs`<br>`MacroGridManager.cs`<br>`EventManager.cs` | Bauen = 1 Tick, Raum-Schaden = 1 Tick + Albtraum, rote Tür-Warnung (`SetDoorWarning`) vor Boss-Zielen[cite: 1, 4]. | `enterClearedRoomFocusCost` im Inspector fest auf `1` setzen (damit **jeder** Raumwechsel 1 Tick kostet und `visitedRooms` komplett entfallen kann); passiven `nightmarePerTurn` auf `0` setzen. |
| **Schrein-Teleporter & Bannkreis**<br>`PlacementManager.cs` | Basis-Erkennung von `BlockModifier.Shrine` mit sofortiger Albtraum-Senkung beim Bauen[cite: 1]. | Interaktives Schrein-Objekt im Raum (Loot + Teleport-UI), Tick-Cooldown, **8-Felder-Reichweitenlimit**, **Boss-Bannkreis** und Boss-Korrumpierung. |
| **Deadlock-Schutz (Gegner & $1\times 1$-Kompression)** | Zerstörungs-Backup (`FindNearestBuiltRoom`), falls der Boss den Raum direkt unter dem Slime sprengt[cite: 1]. | 1. Waffenloser **Würfel-Träger-Gegner** (Dash-Schemen) mit 100-%-Spawn bei 0 Vorrat.<br>2. **Würfel-Kompression** im `PlacementManager` (Taste drücken, um 6er-Würfel als $1\times 1$-Brücke oder Kreuz zu legen). |