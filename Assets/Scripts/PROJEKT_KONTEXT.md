# Slimily – Spieldesign-Konzept, Kern-Systeme & Entwicklungsstand

Das Konzept von **Slimily** vereint die räumliche Taktik eines Grid-Builders mit dem nahtlosen Bewegungsfluss eines präzisen 2D-Platformers[cite: 1, 2]. Es gibt keine künstlichen Runden-Zwangsstopps, kein klassisches Kartendeck-Menü, keine Ladebildschirme zwischen Räumen und keine erzwungenen Kamerawechsel[cite: 1, 2].

---

## 1. Dual-Layer-Prinzip, 360°-Platforming & Raumübergänge (Mikro-Ebene)

* **Einheitliche Perspektive (Seitenansicht):** Makro-Übersichtskarte (Grid) und Mikro-Platformer-Räume nutzen dieselbe orthogonale 2D-Ausrichtung[cite: 1, 2]. Eine Kachel im Norden auf dem Grid entspricht exakt dem oberen Ausgang an der Raumdecke[cite: 1, 2].
* **Nahtloses 360°-Klettern & Ecken-Kontrolle:** Der Slime haftet an Böden, Wänden und Decken, rollt kontrolliert um $90^\circ$-Innenecken sowie $270^\circ$-Außenecken und kann direkt auf scharfen Kanten im $45^\circ$-Winkel anhalten oder diagonal von ihnen abspringen[cite: 1, 2].
* **Volles Movement-Repertoire:** Neben dem 360°-Oberflächen-Klettern und variablen Sprüngen (inkl. Coyote Time, Jump Buffering und Apex Control) verfügt der Slime über **Run (Sprint)**, **Dash**, **Double Jump (Doppelsprung)** und **Float (Ballon-Schweben)**[cite: 1, 2].
* **Räumliche Trennung gegen Überlappung (`roomSpacing`):** Damit Räume beliebig geformt sein können (z. B. L-Formen, große Türme, überlange Decken) und vom Boss auf dem Grid frei verschoben werden können, ohne sich in der 2D-Welt jemals gegenseitig zu schneiden, werden die Raum-Instanzen weit voneinander getrennt platziert (`roomSpacing = 200f`).
* **Präzise 360°-Tür-zu-Tür-Übergänge (`ExecuteRoomTransition`):**
  * **Türrahmen-Geometrie statt starrer Punkt-Teleports:** `Room.cs` misst automatisch die exakten 4×2-Bounding-Boxen der Tür-Blöcke (`Door_North/South/East/West`) aus und passt die `Exit_...`-Trigger an die volle Türbreite an.
  * **Klettern über Raumgrenzen (Boden, Wand & Decke):** Klettert der Slime kopfüber an der Decke oder seitlich an einer Schachtwand durch eine Tür, wird er im Zielraum direkt in die innere Hälfte des dortigen Türrahmens gesetzt, rastet per Raycast (`SnapToDoorFrameSurface`) nahtlos an derselben Decke/Wand ein und nimmt seine Kletterbewegung (`PreserveTraversalThroughDoor`) ohne Kontaktverlust mit.
  * **„Hollow Knight“-Auswurf an Nord-Türen:** Springt oder dasht der Slime in der Luft durch eine obere Tür (`Direction.North`), erhält er im neuen Raum auf Fußbodenhöhe einen konstanten Aufwärtsschub (`northJumpExitVerticalSpeed`) kombiniert mit einem garantierten Seitwärts-Impuls (`northJumpExitHorizontalSpeed`) nach links oder rechts, sodass er immer sicher auf dem festen Rand neben dem Durchgang landet.
* **Waffenloses Gameplay:** Der Slime besitzt keine direkten Waffen oder Angriffswerte[cite: 1, 2]. Gefahren, Beute-Gegner und Bosse werden ausschließlich durch präzises Movement, Raum-Mechanismen, Überlebensphasen und kluges Bauen auf dem Grid überwunden[cite: 1, 2].

---

## 2. Das Tick- & Bedrohungssystem (Aktionssymbole oben rechts)

Statt eines „Zug beenden“-Buttons reagiert die Welt im Takt der Spieleraktionen über **dynamische Countdown-Symbole am oberen rechten Bildschirmrand**[cite: 1, 2].

### 2.1 Die einheitliche Tick-Regel
* **0 Ticks (Kostenlos):** Freies Klettern, Dashen, Springen, Schweben und Looten innerhalb des aktuellen Raums[cite: 1, 2].
* **1 Tick (Raumwechsel):** Jedes Durchschreiten einer Tür in einen Nachbarraum[cite: 2].
* **1 Tick (Standard-Bauen):** Einen 6er-Würfel aus dem Inventar auf das Makro-Grid setzen (spezielle Risiko- oder Fluch-Würfel können 0 bis 2 Ticks kosten)[cite: 1, 2].
* **1 Tick (Altar-/Schrein-Teleport):** Lokaler Sprung von einem aktiven Altar zu einem anderen Altar in Reichweite[cite: 2].
* **1 Straf-Tick (+ Albtraum-Anstieg):** Im Platformer von einem Hindernis oder Gegner getroffen werden (`TakeDamage`)[cite: 1, 2].

### 2.2 Aufbau & Verhalten der Aktionssymbole
* **Dynamische Anzahl (0 bis X):** Oben rechts werden nur dann Medaillons eingeblendet, wenn tatsächlich eine Aktion von Boss, Minion oder Umwelt angekündigt ist[cite: 1, 2].
* **Visuelle Kodierung (Wer, Was, Wann, Wo):** Rahmenform = Urheber (Boss/Minion/Umwelt), Innensymbol = Effekt, Zahl = verbleibende Ticks, Farbe = Farbmarkierung der Zielkacheln auf dem Grid[cite: 1, 2].
* **R2-Inspektor & Live-PiP-Kamera:** Mit `R2` (`Tab`) lassen sich die Symbole inspizieren[cite: 1, 2]. Fällt ein Zähler auf `0`, öffnet sich oben rechts automatisch das **Live-PiP-Fenster** (2,5 s Haltezeit + 1,5 s sanfter Fade-Out) und zeigt live den Einschlag/Zug auf dem Grid, während der Spieler im Raum unterbrechungsfrei weiterläuft[cite: 1, 2].

---

## 3. Biom-Resonanz, Biom-Raumpools & Albtraumleiste

### 3.1 Ruhiges, dunkles Grid & Biom-Farben
* Unbebaute Felder im Nebel (`CellState.Empty`) sind einheitlich dunkelgrau gehalten; nur der Start-Hub und der lila/schwarze Außenrand (`PurpleNightmare` / `BlackVoid`) sind vorab sichtbar[cite: 1, 2].
* Jede Kachelfarbe steht direkt für ein Biom (🌲 **Wald** = Grün, 🏛️ **Ruinen** = Gold-Ocker, 🌊 **Meer** = Blau, 🧪 **Sumpf** = Gift-Türkis, 🟣 **Albtraum** = Violett)[cite: 2].

### 3.2 Biom-Resonanz in festen 20-%-Stufen (`BiomeResonanceManager`)
* Arbeitet in glatten **20-%-Schritten** (`0 %` bis `100 %`), wobei `0 %`-Biome im HUD ausgeblendet werden[cite: 2].
* **Start im Hub:** `🌲 Wald 100%` (innerhalb von `hubSafeRadius = 2` bleibt der Wald stabil)[cite: 2].
* **Verschiebungs-Trigger:**
  1. **Laufrichtung:** Außerhalb des Hub-Radius verschiebt Raumfortschritt die Resonanz um `+20 %` in Richtung der Himmelsrichtung (Norden = *Ruinen*, Osten = *Meer*, Westen = *Sumpf*, Süden = *Wald*)[cite: 2].
  2. **Boss-Puls:** Bei Countdown `0` drückt der Boss `+20 %` seines Bioms (z. B. *Albtraum*) in die Leiste[cite: 2].
  3. **Albtraum-Überlauf:** Ab `60 %` Albtraum drückt automatisch `20 % Albtraum` in die Resonanz[cite: 2].
* **`[Gebaut-Zähler]`:** Zeigt neben dem Prozentwert an, wie viele Kacheln dieses Bioms bereits auf dem Grid gebaut wurden[cite: 1, 2].

### 3.3 Dynamische Biom-Raumpools mit Auto-Refill (`MacroGridManager`)
* **Ordner-Synchronisation:** `MacroGridManager` lädt Raum-Prefabs automatisch aus den Unterordnern unter `Assets/Prefabs/Rooms/<Biom>` (erkennt sowohl englische `ZoneType`-Namen wie `Forest`, `Ruins`, `Ocean`, `Swamp`, `PurpleNightmare` als auch deutsche Ordnernamen).
* **Ziehen ohne Zurücklegen:** Beim Platzieren eines 6er-Würfels zieht jeder der 6 Blöcke anhand seines ausgewürfelten Resonanz-Bioms (`assignedZone`) ein zufälliges Raum-Prefab aus dem jeweiligen Biom-Pool.
* **Auto-Refill & Fallback:** Ist der Pool eines Bioms leer gespielt, wird er automatisch wieder mit allen Räumen dieses Bioms befüllt und neu gemischt. Existiert für ein Biom noch kein Ordner oder kein Raum, greift nahtlos das `defaultRoomPrefab`.

### 3.4 Die Albtraumleiste (0 % – 100 %)
* Steigt nicht passiv pro Runde, sondern nur durch reale Fehler/Gefahren (Treffer im Raum, Boss-Einschlag, Raumzerstörung unter dem Slime, Aufenthalt im lila/schwarzen Rand)[cite: 1, 2].
* Erhöht schrittweise die Feindseligkeit der Welt; bei **100 %** endet der Run (Erwachen im Hub)[cite: 1, 2].

---

## 4. Physisches Würfel-Inventar, Raum-Beute & Level-Editor

### 4.1 Physisches Inventar & „1 + 1“-Beutesystem
* **Kein Kartendeck:** Der Spieler trägt bis zu **5–8 gefundene Würfel** gleichzeitig im Inventar[cite: 2].
* **Farb-Snapshot beim Einsammeln:** In dem Moment, in dem der Slime im Raum ein `CubePickup` einsammelt, würfelt jeder der 6 Blöcke anhand der aktuellen Biom-Resonanz seine Biom-Farbe aus (dreht sich im Baumodus mit `Q`/`E` bzw. `L1`/`R1` exakt mit)[cite: 1, 2].
* **Beute-Verteilung (`CubeLootManager`):** Pro gelegtem 6er-Würfel spawnt in einem der 6 neuen Räume **1 garantierter Weg-Würfel** (`normalCubeSpawnPoints`) und in einem anderen der 6 Räume **1 versteckter Bonus-Würfel** (`secretCubeSpawnPoints`)[cite: 2].

### 4.2 Eigener In-Unity Level-Editor (`Tools -> Slimily Room Painter`)
* Erlaubt das schnelle Zeichnen von Räumen direkt in Unity per Maus oder Rechteck-Tool (⬛ Wand `#`, 🟥 Stacheln `^`, 🟦 Normaler Würfel-Spawn `C`, 🟨 Geheim-Spawn `X`, ⬜ Luft `.`) sowie den Import/Export als **ASCII-JSON** (z. B. für KI-generierte Raum-Grundgerüste).
* Übernimmt automatisch bestehende Tiles/RuleTiles aus einem Vorlagen-Raum (wie `Room1`) oder erzeugt Fallback-Tiles.
* Generiert auf Knopfdruck ein sofort einsatzbereites Prefab im passenden Biom-Ordner (`Assets/Prefabs/Rooms/<Biom>`) mit der exakten Spiel-Hierarchie:
  * `Grid` (`Doors/Door_North..East` inkl. 4×2-Tilemap & Collider, `Tilemap` auf Layer `Ground`, `Hazards` mit Tag `Hazard`)
  * `Exits` (`Exit_North..East` mit `BoxCollider2D` & `RoomExitTrigger`)
  * `Spawns` (`Spawn_North..East` sowie alle `Spawn_Normal_...` und `Spawn_Secret_...`)
  * `LevelBounds` (Trigger-Collider für den `CinemachineConfiner2D`)

---

## 5. Nächster Entwicklungsschritt: Das Altar- / Schrein-System (Konzept & Offene Fragen)

Altäre/Schreine (`BlockModifier.Shrine`) befinden sich als besonderes Symbol auf einzelnen Blöcken bestimmter 6er-Würfel[cite: 1, 2]. Sobald der Spieler einen solchen Würfel auf das Grid legt, entsteht in diesem Raum ein physischer Altar, um den Spieler und Boss aktiv konkurrieren[cite: 2].

### 5.1 Geplante Kern-Funktionen für den Spieler
1. **Physisches Altar-Objekt im Raum:** Statt den Albtraum schon beim bloßen Bauen auf dem Grid zu senken, muss der Slime im Platformer zum Altar klettern und ihn aktivieren.
2. **Belohnung bei Aktivierung:**
   * Senkt die **Albtraumleiste** (z. B. `-10 %` bis `-15 %`)[cite: 2].
   * Spawnt **1 frischen Beute-Würfel** (optional abhängig vom Biom des Altar-Blocks)[cite: 2].
   * Schaltet diesen Raum als **Teleporter-Knotenpunkt** frei[cite: 2].
3. **Schutz gegen 50-Felder-Cheese (Kampfgebiet lokal halten):**
   * **Begrenzte Sprung-Reichweite:** Ein Altar verbindet sich nur mit anderen Altären innerhalb eines festen Radius (z. B. **maximal 8 Grid-Felder**). Alte Altäre aus früheren Gebieten am anderen Kartenende sind außer Reichweite[cite: 2].
   * **Boss-Bannkreis (Arena-Lock):** Sobald ein Boss im Gebiet aktiv ist, blockiert sein Bannkreis jedes Heraus-Teleportieren aus dem Kampfgebiet (auch zurück zum Hub), bis der Boss besiegt ist[cite: 2]. Innerhalb des Bannkreises bleiben lokale Altäre als taktische Ausweich-Punkte (z. B. zur Flucht aus $3\times 3$-Boss-Zonen) voll nutzbar[cite: 2].

### 5.2 Offene Design-Entscheidungen (Vor der Implementierung zu klären)

* **Frage 1: Tick-Cooldown vs. Feste Ladungen?**
  * *Option A (Tick-Cooldown):* Nach der Nutzung (Loot oder Teleport) geht der Altar für z. B. **6 Ticks** auf Cooldown[cite: 2]. Läuft der Cooldown ab, leuchtet er auf dem Grid wieder auf und generiert einen neuen Würfel. *(Gefahr von Farming, falls nicht begrenzt).*
  * *Option B (Feste Ladungen / Erschöpfung):* Jeder gebaute Altar besitzt exakt **2 Ladungen** (z. B. 1× Erwecken/Würfel-Loot + 1× Teleport-Sprung). Danach erlischt der Altar dauerhaft. *(Verhindert endloses Ping-Pong und zwingt dazu, in neuen Gebieten neue Schrein-Würfel zu verbauen).*
  * *Option C (Kombi):* Unbegrenzte lokale Teleports (mit 4 Ticks Cooldown pro Sprung), aber der **Beute-Würfel** spawnt nur **ein einziges Mal** beim ersten Aktivieren.
* **Frage 2: Wie genau interagiert der Boss mit den Altären?**
  * *Idee A (Boss-Korrumpierung):* Ein Boss-Angriff polt einen Altar um (leuchtet violett/rot)[cite: 2]. Er blockiert Teleports und drückt bei jedem Boss-Puls Albtraum rein, bis der Spieler zu Fuß in den Raum klettert und ihn im 360°-Parcours reinigt (Belohnung: neuer Würfel).
  * *Idee B (Würfel-Raub):* Liegt auf einem Altar ein ungesammelter Würfel und der Boss erreicht/visiert das Feld an, frisst der Boss den Würfel und drückt dem Spieler stattdessen einen verfluchten lila Würfel ins Inventar[cite: 2].
  * *Idee C (Teleporter-Kaperung):* Läuft ein mobiler Boss auf einen aktiven Altar, nutzt er das Netzwerk selbst und bricht aus dem Altar hervor, der dem Slime am nächsten liegt.
* **Frage 3: Wie wählt der Spieler das Teleport-Ziel aus?**
  * *Variante 1 (Direkt am Altar über die MapView):* Steht der Slime am Altar und drückt „Interagieren“, öffnet sich die Makro-Kartenansicht (`MapView`), auf der alle erreichbaren Altäre im 8-Felder-Radius hervorgehoben sind (Auswahl per Stick/Maus $\rightarrow$ `1 Tick` $\rightarrow$ Slime spawnt am Ziel-Altar).
  * *Variante 2 (Schnell-Zyklus im Raum):* Direkt über dem Altar schwebt ein kleines Richtungs-Symbol zum nächstgelegenen verbundenen Altar für einen 1-Klick-Sofortsprung ohne Kartenwechsel.
* **Frage 4: Sollen Altäre je nach Biom-Farbe unterschiedliche Boni geben?**
  * Z. B. 🌲 *Wald-Altar* = starke Albtraum-Heilung (`-15 %`); 🏛️ *Ruinen-Altar* = erhöht alle Boss-Timer oben rechts um `+2 Ticks`; 🌊 *Meer-Altar* = nächster Teleport kostet `0 Ticks`.

---

## 6. Weitere Geplante Systeme (Deadlock-Schutz & Raum-Mods)

* **Waffenlose „Würfel-Träger“-Gegner:** Flucht-Schemen im Raum, die man mit dem Dash durchschlagen muss, um einen Würfel zu erhalten (mit 100-%-Gummiband-Spawnrate, falls dem Spieler jemals alle Würfel ausgehen sollten)[cite: 2].
* **Würfel-Kompression ($1\times 1$-Flickstück):** Möglichkeit im Baumodus, einen 6er-Würfel zu opfern, um eine einzelne $1\times 1$-Lücke (z. B. nach Boss-Zerstörung) zu überbrücken[cite: 2].
* **Situative Raum-Modifikatoren:** 🌑 *Finsternis (Blackout)*, ⏳ *Survival-Kammer* (Türen kurzzeitig verriegelt), 👁️ *Wächter-Raum* und ⚡ *Instabil (Kollaps)*[cite: 2].

---

## 7. Entwicklungsstand: Was ist umgesetzt vs. Was steht als Nächstes an?

| Bereich / Modul | ✅ Bereits im Code umgesetzt | 🛠️ Noch offen / Nächste Schritte |
| :--- | :--- | :--- |
| **1. 360°-Movement & Raumübergänge**<br>`PlatformerSlimeController.cs`<br>`Room.cs` / `MacroGridManager.cs` | 360°-Klettern, $90^\circ$/$270^\circ$-Ecken, Dash, Double Jump, Sprint & Ballon-Float[cite: 1, 2].<br>**Neu:** Automatische 4×2-Türrahmen-Erkennung (`GetDoorTunnelWorldBounds`), nahtloses Weiterklettern an Decke/Wand beim Raumwechsel & „Hollow Knight“-Aufwärtssprung an Nord-Türen. | Feintuning der Sprung-/Auswurf-Parameter beim Spielen größerer Test-Cluster. |
| **2. Level-Editor & Biom-Raumpools**<br>`RoomPainterWindow.cs`<br>`MacroGridManager.cs`<br>`PlacementManager.cs` | **Neu:** Eigener In-Unity **Room Painter** (Maus-Pinsel + JSON-Import/Export) erzeugt komplette Prefabs im `Room1`-Format.<br>**Neu:** Automatische **Biom-Raumpools** aus `Assets/Prefabs/Rooms/<Biom>` passend zur Biom-Resonanz (Ziehen ohne Zurücklegen + Auto-Refill + Default-Fallback). | Weitere abwechslungsreiche Räume pro Biom mit dem Room Painter zeichnen; eigene RuleTiles für die Biome erstellen. |
| **3. Physisches Inventar & Beute**<br>`InventoryManager.cs`<br>`CubeLootManager.cs`<br>`CubePickup.cs` | Kartendeck entfernt; physisches Inventar; pro gelegtem 6er-Würfel spawnen **1 garantierter + 1 versteckter Würfel** an den Spawnpunkten der Räume (inkl. Live-Switch per Taste `P`)[cite: 2]. | Optional: Kleine Vorschau-Markierung auf dem Würfel-Block, der den Schrein oder Spezial-Mod trägt. |
| **4. Biom-Resonanz & HUD (Oben links)**<br>`BiomeResonanceManager.cs`<br>`ThreatOrbsAndPiPHUD.cs` | Feste **20-%-Schritte**, Ausblenden von `0 %`-Biomen, Hub-Schutzradius (`hubSafeRadius = 2`), Richtungs-Shift, Boss-Shift (`+20 %`), Albtraum-Überlauf ab `60 %` und `[Gebaut-Zähler]`[cite: 2]. | Visuelles Ambiente-Feedback im Raum bei hohem Albtraum. |
| **5. Aktionssymbole & PiP (Oben rechts)**<br>`ThreatTrackerManager.cs`<br>`BossController.cs` | Dynamische Countdown-Medaillons, `R2`-Inspektion, automatisches **Live-PiP-Fenster** bei Countdown `0` (2,5 s Hold + 1,5 s Fade-Out), rote Tür-Warnung (`SetDoorWarning`)[cite: 1, 2]. | `enterClearedRoomFocusCost` auf `1` und `nightmarePerTurn` auf `0` stellen[cite: 2]; Boss-Angriffe auf echte $3\times 3$-Zonen um den Slime erweitern. |
| **6. NÄCHSTER FOKUS: Altäre / Schreine**<br>`PlacementManager.cs`<br>`GridCell.cs` / `ShrineInteractable.cs` | Bisher nur einfache Sofort-Heilung beim Bauen (`BlockModifier.Shrine`)[cite: 2]. | **Als Nächstes:** Sichtbare Schrein-Markierung auf Grid & Vorschau, interaktiver Altar im Raum (Loot + Heilung), lokaler Teleport (8-Felder-Radius), Cooldown/Ladungen, Boss-Bannkreis & Boss-Korrumpierung[cite: 2]. |
| **7. Deadlock-Schutz & Raum-Mods** | Notfall-Rückwurf (`FindNearestBuiltRoom`), wenn der Boss den Raum unter dem Slime zerstört[cite: 1, 2]. | Waffenloser „Würfel-Träger“-Gegner (Dash-Schemen), $1\times 1$-Würfel-Kompression im Baumodus, situative Raum-Mods (*Blackout*, *Survival*)[cite: 2]. |