# Projekt-Kontext: Slimily

## 1. High-Level Spielkonzept & Genre
* **Genre:** Hybrid aus rundenbasiertem Makro-Dungeon-Builder (Grid/Deck) und kinetischem 2D-Mikro-Platformer ohne Kampfsystem (reines Geschicklichkeits- und Movement-Gameplay).
* **Kern-Gameplay-Loop:**
  * **Makro-Ebene:** Aus dem Inventar Polyomino-/Tetris-förmige Raum-Shapes auf einem Raster platzieren, Pfade erschaffen und Rundenzüge verwalten, während ein Boss gegnerische Züge ausführt.
  * **Mikro-Ebene:** In Echtzeit mit dem Slime-Charakter durch die platzierten Räume navigieren, Hindernisse überwinden und Türen/Ausgänge (z. B. Nordtür) erreichen.

---

## 2. Architektur & System-Aufteilung

### Makro-Ebene (Grid / Planung / Runden)
* **GameManager:** Zentraler State-Machine-Verwalter (`GameState`: u. a. `Inventory`, `Placement`, `FreeRoam`, `EnemyTurn`).
* **GridManager & GridCell:** Verwaltet das globale Daten-Raster, Zellzustände (`Empty`, `Occupied`, `Blocked`, `Built`) und Baufähigkeitsregeln.
* **PlacementManager:** Handhabt das Auswählen, Rotieren (Rechtsklick), Vorschau-Rendern und finale Platzieren von `ShapeData`-Strukturen auf dem Grid inklusive Rundenlimit pro Zug.
* **InventoryManager:** Verwaltet verfügbare Shapes im Deck/Inventar und sorgt für Nachschub.
* **MacroGridManager:** Koppelt logische Grid-Koordinaten mit physischen Platformer-Raum-Instanzen (`RegisterRoom`, `UpdateActiveRooms`).
* **EventManager:** Steuert die Rundenabfolge (Spielerzug abschließen, Gegner-/Bosszug initiieren, Reset der Platzierungslimits).
* **BossController:** Führt in der Gegnerphase autonome Raster-Aktionen und Bewegungen in Richtung des Makro-Slime-Markers durch.

### Mikro-Ebene (Platformer / Bewegung)
* **PlatformerSlimeController:** Zentrale Steuerungseinheit des spielbaren Slimes im 2D-Platformer.
  * Arbeitet mit einem modularen State-Machine-Pattern (`PlatformerStateMachine`, `PlatformerSlimeState`).
  * Nutzt deterministische, vollständig kinematisierte Auflösung via `BoxCast` und `Raycast` (Unity-Rigidbody rein auf `Kinematic` / `FreezeRotation` gestellt).
  * Berechnet dynamisch Oberflächennormalen und zyklische Bewegungstangenten (`Vector2(normal.y, -normal.x)`), um unterbrechungsfreie 360°-Navigation im und gegen den Uhrzeigersinn zu ermöglichen.
  * **Kardinales Normalen-Snapping (`SnapToCardinal`):** Rundet gemessene Treffernormalen hart auf Himmelsrichtungen ab, um Sprite-Unregelmäßigkeiten und Kanten-Dellen von Tilemaps auszubügeln.
  * **Lookahead- & Prioritätsprüfung:** Frontale Wände/Decken (Innenecken) besitzen strikte Erkennungspriorität vor Außenecken, um Tunneln oder Fehlausrichtungen bei eng beieinander liegenden Eckenfolgen (z. B. U-Shapes) auszuschließen.
* **States:**
  * **PlatformerGroundedState:** Kontinuierliche Bewegung entlang von Boden, Wänden und Decke. Enthält das **Sticky Momentum** (konsistente Umlaufrichtung trotz wechselnder Achsen bei gehaltener Taste), **Corner-Wrapping** um $90^\circ$-Innenwinkel und $270^\circ$-Außenkanten sowie kontextabhängiges Lösen von der Decke (Dropdown via `S`, gesperrt während Wandabstiegen).
  * **PlatformerAirborneState:** Ballistische Flugphase mit Schwerkrafteinfluss, Pufferung horizontalen Impulses (Carry-Over-Speed bei Landung/Absprung) und selektivem Flächen-Snapping.
* **PlatformerPhysicsData:** ScriptableObject zur Kapselung aller physischen Tuning-Parameter (Geschwindigkeiten, Sprungkräfte, Check-Distanzen, Layer-Masks, Skin-Breite).

### Brücke / Transition (Makro $\leftrightarrow$ Mikro)
* **Kamera-Wechsel:** Umschaltung zwischen Makro-Ansicht (Karten-Übersicht) und Mikro-Ansicht (Slime-Platformer) über dedizierte Kameras (MacroCamera vs. Raum-/Slime-Kamera).
* **Input-Filterung:** Saubere Entkopplung je nach aktuellem `GameState` (keine Doppelbelegungen).
* **Positions-Synchronisation:** Die physische Position des Slimes aktualisiert synchron den logischen Marker auf dem Makro-Raster (`MacroSlimeMarker`).

---

## 3. Bereits gelöste / Etablierte Mechaniken

* **Robuste 360°-Oberflächen-Physik:** Zitterfreies Klettern an Wänden und Decken ohne Abrutschen oder Ausdauer-Limits.
* **Nahtlose Ecken-Navigation:** Automatisches Umlaufen von $90^\circ$-Innenwinkeln und $270^\circ$-Außenkanten mit mathematischer Scheitelpunkt-Berechnung und Skin-Width-Kompensation.
* **Sticky-Momentum-System:** Komplettes Umrunden geschlossener oder komplexer Geometrien (inkl. U-Profilen) mit einer einzigen gehaltenen Richtungstaste.
* **Flächen-relatives Springen & Lösen:**
  * Regulärer Sprung am Boden mit Erhalt des horizontalen Momentums.
  * Wandabsprünge schräg nach außen/oben.
  * Kontrollierter Decken-Abstoß nach unten via Leertaste bzw. weicher Dropdown via `S`.
  * **Richtungsabhängige Absprung-Sperre (`CanAttachTo`):** Verhindert das Re-Sticking an der Absprungwand, erlaubt aber das sofortige Anhaften an Decken oder Nachbarwänden ohne Cooldown-Verzögerung.
* **Responsives Jump-Buffering:** Verzeihendes Input-Zeitfenster vor der Landung ohne Geschwindigkeits-Abriss beim Wiederabsprung.
* **CompositeCollider2D-Stabilität:** Zuverlässige Erkennung trotz Tilemap-Verschmelzung via angehobener Sensor-Casts und Laser-Raycasts.

---

## 4. Offene Baustellen & Nächste Schritte

* **Terrain-Spezifikationen (Non-Stick & Slippery):**
  * Einführung spezieller Layer oder Tile-Klassen für Flächen, an denen der Slime **nicht** haften kann (Abrutschen an Eis/Metall), um Kletterrouten bewusst zu kanalisieren.
* **Gefahren- & Respawn-System (Hazards):**
  * Stacheln, Säure oder Hindernis-Trigger mit unmittelbarem Reset an den Raum-Spawn (`Spawn_North/South/West/East`).
* **Erweiterte Mobilitäts-Erprobung (nach Bedarf im Level-Test):**
  * Aufbläh- / Schwebemechanik (Fallschirm-Modus zur Sturzkontrolle).
  * Flächen- oder Air-Dash zur Überbrückung nicht-bekletterbarer Lücken.
* **Visuelles Feedback & Polish:**
  * Squash & Stretch beim Aufprall und Absprung.
  * Partikeleffekte (Schleimtröpfchen an Kontaktflächen).