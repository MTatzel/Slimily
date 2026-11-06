# Projekt-Kontext: Slimily

## 1. High-Level Spielkonzept & Genre
- **Genre:** Hybrid aus rundenbasiertem Makro-Dungeon-Builder (Grid/Deck) und kinetischem 2D-Mikro-Platformer ohne Kampfsystem (reines Geschicklichkeits-, Timing- und Movement-Gameplay).
- **Kern-Gameplay-Loop:**
  * **Makro-Ebene:** Aus dem Inventar Polyomino-/Tetris-förmige Raum-Shapes auf einem Raster platzieren, Pfade erschaffen und Rundenzüge verwalten, während ein Boss gegnerische Züge auf dem Grid ausführt.
  * **Mikro-Ebene:** In Echtzeit mit dem Slime-Charakter durch die platzierten Räume navigieren, Hindernisse überwinden und Türen/Ausgänge (z. B. Nordtür) erreichen.

---

## 2. Architektur & System-Aufteilung

### Makro-Ebene (Grid / Planung / Runden)
- **GameManager:** Zentraler State-Machine-Verwalter (`GameState`: u. a. `Placement`, `FreeRoam`, `EnemyTurn` und neu geplant: `MapView`).
- **GridManager & GridCell:** Verwaltet das globale Daten-Raster, Zellzustände (`Empty`, `Occupied`, `Blocked`, `Built`) und Baufähigkeitsregeln.
- **PlacementManager:** Handhabt das Auswählen, Rotieren, Vorschau-Rendern und finale Platzieren von `ShapeData`-Strukturen auf dem Grid inklusive Rundenlimit pro Zug.
- **InventoryManager:** Verwaltet verfügbare Shapes im Deck/Inventar und sorgt für Nachschub.
- **MacroGridManager:** Koppelt logische Grid-Koordinaten mit physischen Platformer-Raum-Instanzen (`RegisterRoom`, `UpdateActiveRooms`).
- **EventManager:** Steuert die Rundenabfolge (Spielerzug abschließen, Gegner-/Bosszug initiieren, Reset der Platzierungslimits).
- **BossController:** Führt in der Gegnerphase autonome Raster-Aktionen und Bewegungen in Richtung des Makro-Slime-Markers durch.

### Mikro-Ebene (Platformer / Bewegung & Input)
- **PlatformerSlimeController:** Zentrale Steuerungseinheit des spielbaren Slimes im 2D-Platformer.
  * Arbeitet mit modularem State-Machine-Pattern (`PlatformerStateMachine`, `PlatformerSlimeState`).
  * Deterministische, vollständig kinematisierte Auflösung via `BoxCast` und `Raycast` (Unity-Rigidbody auf `Kinematic` / `FreezeRotation`).
  * Dynamische Berechnung von Oberflächennormalen und zyklischen Bewegungstangenten (`Vector2(normal.y, -normal.x)`) für unterbrechungsfreie 360°-Navigation im/gegen den Uhrzeigersinn.
  * **Kardinales Normalen-Snapping (`SnapToCardinal`):** Rundet gemessene Treffernormalen hart auf Himmelsrichtungen ab, um Sprite-Unregelmäßigkeiten und Kanten-Dellen von Tilemaps auszugleichen.
  * **Lookahead- & Prioritätsprüfung:** Frontale Wände/Decken (Innenecken) besitzen strikte Priorität vor Außenecken, um Fehlausrichtungen bei engen Eckenfolgen (z. B. U-Shapes) auszuschließen.
  * **Zentralisierter Hybrid-Input (Unity New Input System):**
    * Parallele Unterstützung von Tastatur und Gamepad (PS5 DualSense / `Gamepad.current`).
    * **Radial Deadzone & Achsen-Snapping:** Filtert Schräglauf-Rauschen am Analogstick heraus, um versehentliche Richtungsabbrüche an Ecken zu verhindern; D-Pad und Stick arbeiten additiv/priorisierend.
- **States:**
  * **PlatformerGroundedState:** Kontinuierliche Bewegung entlang von Boden, Wänden und Decke. Enthält **Sticky Momentum**, **Corner-Wrapping** um $90^\circ$-Innen- und $270^\circ$-Außenwinkel.
  * **PlatformerAirborneState:** Ballistische Flugphase mit Schwerkrafteinfluss, Carry-Over-Speed bei Landung/Absprung und selektivem Flächen-Snapping.
- **PlatformerPhysicsData:** ScriptableObject zur Kapselung physischer Tuning-Parameter (Geschwindigkeiten, Sprungkräfte, Check-Distanzen, Skin-Breite, Hold-Timer).

### Brücke / Transition (Makro $\leftrightarrow$ Mikro)
- **Kamera-Wechsel:** Dynamische Umschaltung zwischen Makro-Ansicht (Karten-Übersicht) und Mikro-Ansicht (Slime-Platformer) über dedizierte Kameras (MacroCamera vs. Raum-/Slime-Kamera).
- **Zwei Makro-Modi auf selbem Grid:**
  1. **Schnelle Kartenansicht (`MapView`):** Reine Orientierungsübersicht (Position Slime, Boss, offene Türen) ohne Bau-Interaktion, dezent abgedunkelt.
  2. **Baumodus (`Placement`):** Vollständiger Editor mit Handkarten, Shape-Rotation und Platzierung.
- **Positions-Synchronisation:** Physische Slime-Position aktualisiert synchron den logischen Marker auf dem Makro-Raster (`MacroSlimeMarker`).

---

## 3. Bereits gelöste / Etablierte Mechaniken

- **Robuste 360°-Oberflächen-Physik:** Zitterfreies Klettern an Wänden und Decken ohne Abrutschen.
- **Nahtlose Ecken-Navigation:** Automatisches Umlaufen von $90^\circ$-Innen- und $270^\circ$-Außenkanten mit mathematischer Scheitelpunkt-Berechnung.
- **Sticky-Momentum-System:** Komplettes Umrunden geschlossener Geometrien mit einer einzigen gehaltenen Richtungseingabe.
- **PS5 Controller-Support:**
  * Vollständig eingebunden über Unitys New Input System.
  * Stick-Deadzone ($0.2f$) und D-Pad-Präzision.
- **Abgesichertes Decken-Lösen (Hold-Timer & Strict Angle):**
  * Verhindert versehentliches Abreißen bei Seitwärtsbewegungen.
  * Erfordert bewusstes Halten des Sticks strikt nach unten ($Y \le -0.8f$, $\vert{}X\vert{} \le 0.25f$) über eine im ScriptableObject konfigurierbare Zeit (`ceilingDetachHoldDuration`, ca. $0.25\text{ s}$).
- **Flächen-relatives Springen & Lösen:**
  * Regulärer Sprung am Boden, Wandabsprung schräg nach außen/oben, Deckenabstoß nach unten.
  * **Richtungsabhängige Absprung-Sperre (`CanAttachTo`):** Verhindert Re-Sticking an der Absprungfläche, erlaubt aber sofortiges Anhaften an Decken oder Nachbarwänden.
- **Responsives Jump-Buffering:** Pufferzeitfenster für Absprünge kurz vor Bodenkontakt.
- **Saubere Projekt-Basis:** `.gitignore` für Unity bereinigt und Caches ausgeschlossen.

---

## 4. Geplantes Controller-Tastenlayout (PS5 DualSense)

### Mikro-Ebene (Slime Platformer)
- **Linker Stick / D-Pad:** $360^\circ$-Movement entlang aller Flächen.
- **Linker Stick strikt nach unten (Hold-Timer):** Decken-Dropdown / Fallenlassen.
- **$\times$ (Cross):** Sprung / Wandabsprung / Deckenabstoß (später: Halten in der Luft = Schweben / Glide).
- **R2 oder $\square$ (geplant):** Dash (kurzer Tap) / Sprint (Halten) nach Silksong-Prinzip.
- **Touchpad:** Schnelle Kartenansicht toggeln / halten (`MapView`).
- **$\triangle$ (Halten / Meditieren):** Wechsel in den Makro-Baumodus (`Placement`).
- **R3 (Stick-Klick):** Spielerzug direkt aus dem Platformer beenden (schaltet Kamera hoch auf Makro, friert Slime ein, lässt Boss ziehen).

### Makro-Ebene (Grid-Baumodus & Map)
- **Linker Stick / D-Pad:** Virtueller Raster-Cursor von Zelle zu Zelle.
- **L1 / R1:** Aktives Raum-Shape im Uhrzeigersinn / gegen den Uhrzeigersinn rotieren.
- **$\times$ (Cross):** Shape auf Raster platzieren.
- **L2 / R2 (oder D-Pad):** Durch Handkarten/Deck blättern.
- **$\triangle$ oder R3:** Spielerzug beenden $\rightarrow$ Gegnerphase/Bosszug starten.
- **$\bigcirc$ (Kreis) oder $\triangle$ (Tap):** Zurück zum Slime (Mikro-Ebene).

---

## 5. Offene Baustellen & Nächste Schritte

1. **Perspektiven- & Zustandswechsel (Fokus als Nächstes):**
   * Saubere State-Trennung im `GameManager` (`MapView` vs. `Placement` vs. `FreeRoam`).
   * Implementierung des „Meditieren“-Hold-Timers auf $\triangle$ zum Betreten des Baumodus.
   * Einbindung des Touchpads für die reine Orientierungs-Karte (ohne Bau-Interaktion).
   * R3-Kopplung für schnelles „Zug beenden“ mit automatischer Kamerafahrt zum Boss.
2. **Controller-Navigation auf dem Makro-Grid:**
   * Entkopplung des PlacementManagers von der PC-Maus hin zum zellbasierten Gamepad-Cursor.
   * Rotation über Schultertasten (L1/R1).
3. **Terrain-Spezifikationen & Kletterrouten:**
   * Non-Stick- / Slippery-Surfaces (Eis/Metall), an denen der Slime nicht haften kann.
4. **Gefahren- & Respawn-System:**
   * Hazards (Stacheln, Säure) mit unmittelbarem Reset an den Raum-Spawn.
5. **Erweiterte Mobilität:**
   * Dash / Sprint auf R2, Schweben/Aufblähen auf $\times$.
6. **Visuelles Feedback & UI-Polish:**
   * 9-Sliced Panels, dezente Farben und universelles Grid-Feedback (Hologramm-/Blueprint-Look).