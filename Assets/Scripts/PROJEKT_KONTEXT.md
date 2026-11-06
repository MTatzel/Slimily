# Slimily (Puls-Edition) – Spieldesign-Konzept & Technische Roadmap

Das überarbeitete Konzept von **Slimily** vereint die taktische Tiefe eines Grid-Deckbuilders mit dem unterbrechungsfreien Spielfluss eines präzisen 2D-Platformers. Durch den Wechsel vom starren „Zug beenden“-Rundenmodell auf das 4-Puls-Rhythmus-System entfallen künstliche Zwangspausen, geschlossene Türen vor der Nase und hektische Kamerawechsel.

---

## 1. Das Gesamtkonzept

### 1.1 Grundprinzip & 360°-Raum-Parcours (Dual-Layer ohne Waffen)

* **Einheitliche Perspektive:** Makro-Übersichtskarte und Mikro-Platformer-Räume nutzen dieselbe orthogonale 2D-Ausrichtung (Norden auf dem Grid entspricht der oberen Tür im Raum).
* **360°-Movement & Ecken-Kontrolle:** Der Slime klettert nahtlos an Böden, Wänden und Decken entlang, rollt kontrolliert um $90^\circ$-Innen- sowie $270^\circ$-Außenecken und kann direkt auf Kanten landen, dort im $45^\circ$-Winkel anhalten oder diagonal abspringen.
* **Räume als 360°-Herausforderung:** Da Decken und Wände jederzeit begehbar sind, sind auch die oberen und seitlichen Raumflächen vollwertige Hindernis-Parcours (mit Decken-Gefahren, Non-Stick-Oberflächen und Engstellen), die präzises Ablösen und Springen verlangen.
* **Waffenloses Gameplay:** Es gibt keine Angriffe oder Schadenswerte. Der Spieler überwindet Gefahren durch Bewegung und besiegt Bosse über Überlebensphasen, Raum-Mechanismen und kluge Grid-Verbindungen.

---

### 1.2 Das 4-Puls-Rhythmus-System (Statt „Zug beenden“ & Kamera-Unterbrechung)

Das Spiel verzichtet komplett auf einen manuellen „Zug beenden“-Button im Platformer. Stattdessen reagiert die Albtraumwelt im Takt deiner Aktionen über einen Puls-Zähler (Standard: 4 Pulse):

#### Was kostet Pulse?
* **1 Puls:** Einen 6er-Würfel auf das Makro-Grid legen.
* **1 Puls:** Einen neuen (noch ungesäuberten) Raum zum ersten Mal betreten.
* **1 Straf-Puls (+ Albtraum-Anstieg):** Im Platformer von einem Hindernis oder Gegner getroffen werden (`TakeDamage`).

#### Was kostet 0 Pulse (Kostenlos)?
* Freies Klettern, Springen und Interagieren innerhalb des aktuellen Raums.
* Das Durchqueren von bereits gesäuberten/besuchten Räumen.

#### Was passiert beim 4. Puls (Der Boss-Schlag)?
* Sobald der 4. Puls verbraucht wird (egal ob durch Bauen, das Betreten eines neuen Raums oder einen Treffer im Platformer), setzt sich der Zähler sofort wieder auf 4/4 zurück und löst im Hintergrund automatisch genau 1 Boss-/Welt-Aktion aus (`ExecuteTurn`).
* **Kein Einfrieren, kein Kamera-Zwang:** Der Slime läuft im Raum flüssig weiter; alle Türen zu gebauten Nachbarräumen bleiben jederzeit offen.

#### Warum Stehenbleiben unmöglich / sinnlos ist:
Da es kein passives „Zug beenden“ mit Gratis-Boni gibt, passiert beim Stehenbleiben im Raum nichts. Fortschritt entsteht nur durch Vorrücken, und jeder Boss-Puls erhöht zusätzlich den Grunddruck der globalen Albtraumleiste (0 % – 100 %). Erreicht die Albtraumleiste 100 %, scheitert der Run und der Slime wacht im Hub auf.

---

### 1.3 Die Welt & Die visuelle 4-Ebenen-Hierarchie

Um ein „RGB-Disco-Chaos“ bei gleichzeitigen Zonenfarben, Raum-Modifikatoren und Boss-Effekten zu verhindern, nutzt das Grid eine düstere, handgezeichnete Ästhetik (verwitterter Stein, Tusche, Moos, Pergament) mit strikter Trennung in 4 visuelle Ebenen:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ Ebene 3: Boss-Gefahren      -> Rote Riss-Schraffur, Fadenkreuz, Ketten  │
├─────────────────────────────────────────────────────────────────────────┤
│ Ebene 2: Akteure            -> Plastischer Slime-Marker & Boss-Monster  │
├─────────────────────────────────────────────────────────────────────────┤
│ Ebene 1: Raum-Eigenschaften -> Weiße/Kreideartige Gravur-Glyphen        │
├─────────────────────────────────────────────────────────────────────────┤
│ Ebene 0: Zone & Status      -> Erdfarbene Steinplatten (Nebel vs Gebaut)│
└─────────────────────────────────────────────────────────────────────────┘
```

1. **Ebene 0 – Zone & Bauzustand (Farbe & Helligkeit):**
   * Im Zentrum liegt der helle, sichere Hub. Darum gruppieren sich 3 Tier-1-Zonen (z. B. Wald = Moosgrün, Meer = Tiefsee-Blau, Ruinen = Ocker-Sandstein) sowie weiter außen bedingte Tier-2-Zonen (z. B. Sumpf am Wald) und der lila/schwarze Albtraum-Rand.
   * Im Nebel schimmern die Zonenfarben dunkel durch den Rauch; gebaute Kacheln sind voll ausgeleuchtete, strukturierte Steinplatten in der jeweiligen Zonenfarbe.
2. **Ebene 1 – Kachel-Modifikatoren (Monochrome Gravuren):**
   * Eigenschaften wie Eis-Physik (Schneeflocke), Schalter-Räume (Zahnrad), Teleporter (Ringe) oder Schreine werden als einfarbige, helle Kreide-/Stein-Symbole auf der Kachel dargestellt und verändern niemals die Grundfarbe der Zone.
3. **Ebene 2 – Akteure:**
   * Der leuchtende Slime-Tropfen und die plastische $2\times 2$-Silhouette des Bosses heben sich durch klare Konturen und Schattenwurf vom Untergrund ab.
4. **Ebene 3 – Boss-Gefahren & Manipulationen (Rahmen, Risse & Kanten):**
   * **Angriffszonen:** Keine deckende rote Fläche, sondern ein rot glühender Rahmen mit diagonaler Schraffur und glimmenden Rissen, durch die Zone und Symbol voll sichtbar bleiben.
   * **Entschärfungs-Epizentrum:** Ein pulsierendes rotes Fadenkreuz markiert das Feld, in dem der Angriff im Raum abgebrochen werden kann.
   * **Verschiebungen & Sperren:** Kleine Doppelpfeile (`>>`) zeigen Kachel-Verschiebungen an; Ketten und Dornenrahmen auf der Kachel-Grenze markieren verriegelte Türen.

---

### 1.4 Bausystem: 6er-Faltwürfel, Bau-Reichweite & Raum-Boni

* **11 echte 6er-Würfelnetze (Hexominos):** Karten falten sich beim Platzieren in 6 zusammenhängende Räume auf und nehmen jeweils die Zone des darunterliegenden Feldes an.
* **Bau-Reichweite (Tether-Regel gegen den Hub-Autobahn-Exploit):** Ein neuer Würfel darf nur platziert werden, wenn mindestens eine seiner 6 Kacheln im Umkreis von maximal 2 Feldern um die aktuelle Position des Slimes (`MacroSlimeMarker`) liegt und an ein gebautes Feld andockt. Der Spieler muss im Platformer vorrücken, um weiter vorne neue Wege anzubauen.
* **Aktive Kachel-Boni im Raum:** Legt man eine Kachel mit einem positiven Effekt (z. B. Schrein zur Albtraum-Senkung oder Teleporter), befindet sich dieser im Platformer-Raum an einer riskanten Position abseits des direkten Durchgangswegs.

---

### 1.5 Umwelt-Telegrafie im Raum & Die 4 Boss-Archetypen

#### Wie der Spieler Boss-Aktionen spürt, OHNE dass die Kamera rauszoomt:
* **Boss agiert in der Ferne:** Kurzes Raum-Beben, dumpfes Grollen und ein Partikel-/Schatten-Schauer aus der Himmelsrichtung des Bosses. Der Spieler läuft ohne Unterbrechung weiter und kann bei Bedarf selbst kurz die Schnellkarte (`MapView` per Touchpad / `M`) antippen.
* **Nachbarraum ist rot markiert:** Die Tür (`RoomDoor`) in diese Himmelsrichtung glüht und raucht im aktuellen Raum rot.
* **Aktueller Raum ist rot markiert:** Glühende Risse überziehen den Raumhintergrund, und oben im HUD warnt ein Countdown („Einschlag hier in X Pulsen!“).
* **Aktives Entschärfen statt Weglaufen:** Da das bloße Zurückweichen in gesäuberte Räume 0 Pulse kostet, bestraft ein nicht verhinderter Boss-Einschlag deine Infrastruktur (zerstört gebaute Kacheln via `RemoveRoomAt` oder korrumpiert sie lila). Dringt der Spieler jedoch rechtzeitig in das Fadenkreuz-Epizentrum vor und betätigt den Unterbrecher im Raum, bricht er den Cast ab (`CancelCast`) und schwächt den Boss.

#### Die 4 Boss-Archetypen (Kein stupides 1:1-Hinterherlaufen)
1. **Die Festung / Der Belagerer (z. B. Ruinen):** Stationärer $2\times 2$-Turm. Nimmt alle 4 Pulse deine gebauten Straßen unter Beschuss, dreht Kacheln oder verriegelt Türen (`LockDoors`), bis du ihn erreichst und im Arena-Raum überlebst.
2. **Der Saboteur / Jäger (z. B. Wald):** Bewegt sich auf deinen gebauten Kacheln gezielt zu deinen Knotenpunkten/Schreinen, um deine Route zu kappen – oder bricht bei Kontakt direkt in deinen aktuellen Platformer-Raum ein.
3. **Das Myzel / Netzwerk (z. B. Sumpf):** Reiner Grid-Boss ohne Monster-Token. Besitzt 3 verteilte Herzkammern im Nebel, die der Spieler über eine durchgehende Kette aus 6er-Würfeln miteinander verbinden („kurzschließen“) und im Raum kappen muss.
4. **Der Mahlstrom (z. B. Meer):** Zieht alle 4 Pulse die unterste Reihe deiner gebauten Kacheln (samt Slime) um 1 Feld Richtung Abgrund.

---

### 1.6 Deckbuilding (Dual-Use-Karten) & Run-Struktur

* **Dual-Use-Karten (Kein „Dead Draw“):** Jede Karte im Deck (Start: 8, Ausbau auf 14–18) kann entweder oben auf dem Grid als 6er-Würfel gebaut werden (1 Puls) oder unten im Platformer für 0 Pulse geopfert werden, um sofort einen Überlebens-Effekt im Raum auszulösen (z. B. Schild gegen nächsten Treffer/Straf-Puls, Türen knacken, Schwerkraft-Impuls).
* **Run-Struktur & Adaptiver Endboss:** Ein Run dauert ca. 20–30 Minuten und führt durch 2–3 Zonen zum Endboss, der die Fähigkeiten genau jener Zonen-Bosse übernimmt, die der Spieler auf seiner Route besiegt hat.

---

## 2. Abgleich: Was ist bereits umgesetzt vs. Was muss geändert werden?

Durch die Vorarbeit in Schritt 1–3 und das Ecken-Update steht bereits ein Großteil der Technik. Der Umbau auf das 4-Puls-System erfordert vor allem das Entfernen der alten Runden-Stopps in drei bestehenden Skripten:

| System / Skript | Aktueller Stand im Code (inkl. Schritt 1–3) | Was jetzt geändert / noch umgesetzt werden muss |
| :--- | :--- | :--- |
| **Steuerung & Ecken**<br>`PlatformerSlimeController.cs`<br>`GroundedState.cs`<br>`AirborneState.cs`<br>`PhysicsData.cs` | **Fertig umgesetzt:** 360°-Klettern, sanftes Rollen & Stehenbleiben auf $270^\circ$-Außenecken, $90^\circ$-Innenecken-Bremse, Variable Jump, Coyote Time, Apex Control, Squash & Stretch sowie `TakeDamage()` mit Blink-Unverwundbarkeit. | **Keine Änderung an der Physik nötig!**<br>Lediglich `TakeDamage()` friert den Slime bei 0 Fokus nicht mehr ein, sondern zieht einfach 1 Puls ab. |
| **Puls-Zähler & Rundenfluss**<br>`GameManager.cs`<br>`EventManager.cs` | **Noch auf altem Stand (Zwangsstopp):** Zählt `currentFocus` von 4 auf 0 herunter. Bei 0 Fokus durch Treffer friert der Slime ein (`SetSlimeActive(false)`), die Kamera fährt zwangsweise hoch in `EnemyTurn` und zeigt `TurnBannerUI`-Banner (ZWANGSSTOPP / GEGNERPHASE / DEIN ZUG). | **Umbau auf 4-Puls-System (Wichtigste Änderung):**<br>1. Zwangsstopp, Slime-Einfrieren und Kamera-Rauszoomen aus dem `FreeRoam` entfernen.<br>2. Sobald `currentFocus` (Pulse) 0 erreicht, füllt er sich sofort wieder auf 4 auf und triggert im Hintergrund `ExecuteBossPulse()`.<br>3. Bist du im Platformer, läuft ein kurzes Kamera-Rumpeln ab statt des Kamerawechsels; bist du im Baumodus (`MacroPlan`), siehst du den Boss-Zug direkt auf dem Grid. |
| **Raumübergänge & Türen**<br>`MacroGridManager.cs`<br>`Room.cs` | **Teilweise umgesetzt:** Merkt sich besuchte Räume in `visitedRooms` (neue Räume kosten 1, besuchte 0). Aber: Aktuell sperrt `UpdateRoomDoors()` alle Türen zu neuen Räumen physisch zu, sobald `currentFocus == 0` ist. | **Kleine Anpassung:**<br>1. Tür-Sperre bei 0 Fokus entfernen (`HasRoom` statt `CanEnterRoomAt`), da sich der Puls-Zähler bei 0 sofort wieder auf 4 zurücksetzt und den Boss-Tick auslöst.<br>2. Wenn ein Nachbarraum auf dem Grid `isTargeted` ist, Tür im Raum optisch rot einfärben (Umwelt-Telegrafie). |
| **Bau-Regeln**<br>`PlacementManager.cs` | **Noch auf altem Stand:** Prüft in `CanPlaceShape()` nur, ob der Würfel irgendwo an ein bereits gebautes Feld (`CellState.Built`) angrenzt $\rightarrow$ erlaubt noch den „Hub-Autobahn-Exploit“. | **Kleine Ergänzung (ca. 10 Zeilen):**<br>Prüfung `maxBuildDistanceFromSlime = 2` in `CanPlaceShape()` einbauen: Mindestens eine Kachel des neuen 6er-Würfels muss $\le 2$ Felder vom aktuellen Raum des Slimes entfernt sein. |
| **Grid, Zonen & 6er-Würfel**<br>`GridManager.cs`<br>`GridCell.cs`<br>`InventoryManager.cs` | **Grundfunktion umgesetzt:** Hub, Tier-1/Tier-2-Zonen, farbiger Nebel, lila/schwarzer Rand und die 6er-Würfelnetze (Hexominos) laufen bereits. | **Späteres Polish (für den ersten Test noch nicht zwingend):**<br>Weiße Modifikator-Icons auf `GridCell` anzeigen und das rote Warn-Overlay als Schraffur/Rahmen statt Vollfarbe darstellen. |
| **Boss-Verhalten & Konter**<br>`BossBase.cs`<br>`BossController.cs` | **Noch auf altem Stand:** Boss läuft in `MoveTowardsSlimeRoutine()` 1 Feld auf den Slime zu und zerstört 1 Feld vor sich (`DestroyRoom`). Notfall-Schleudern bei Raumzerstörung unter dem Slime ist bereits drin. | **Mittelfristiger Ausbau:**<br>Boss markiert $3\times 3$-Gefahrenzonen mit 1 Epizentrum-Feld (Fadenkreuz). Betritt der Slime das Epizentrum vor Ablauf der Pulse, wird der Angriff abgebrochen (`CancelCast`) und der Boss geschwächt. |