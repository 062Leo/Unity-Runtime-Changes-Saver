# Play Mode Transform Overrides (Unity Editor Tools)

Dieses kleine Editor-Toolset ermöglicht es, Transform- bzw. RectTransform-Änderungen,
die im Play Mode vorgenommen werden, gezielt in den Edit Mode zu übernehmen –
ähnlich wie Prefab Overrides im Unity-Inspector.

Die Funktion ist besonders hilfreich, wenn du im Play Mode z.B. UI-Elemente,
Positionen oder Größen anpasst und diese Änderungen nicht verlieren möchtest,
wenn du den Play Mode stoppst.

---

## Überblick

Das Tool besteht aus drei zentralen Skripten:

- **PlayModeChangesInspector.cs**  
  Zeichnet im Inspector (Header-Bereich) ein Panel „Play Mode Overrides“.  
  Dort werden alle veränderten Transform-/RectTransform-Eigenschaften des
  aktuell inspizierten GameObjects angezeigt und können selektiv markiert
  werden.

- **PlayModeChangesTracker.cs**  
  Kümmert sich um das Tracking der Änderungen während des Play Modes.  
  Es speichert Start-Snapshots, berechnet Unterschiede und schreibt/liest
  Daten aus dem Store.

- **PlayModeTransformChangesStore.cs**  
  ScriptableObject, das die ausgewählten Änderungen persistent in einer
  Asset-Datei (`PlayModeTransformChangesStore.asset`) speichert, damit sie
  beim Wechsel zurück in den Edit Mode angewendet werden können.

---

## Funktionsweise 

1. **Beim Start des Play Modes**  
   `PlayModeChangesTracker` erzeugt für alle GameObjects einen
   `TransformSnapshot` und merkt sich deren Ausgangszustand.

2. **Während des Play Modes**  
   Im Inspector-Header siehst du pro GameObject das Panel „Play Mode Overrides“.  
   Dort werden alle tatsächlich veränderten Eigenschaften aufgelistet
   (Position, Rotation, Scale sowie RectTransform-Werte wie `anchoredPosition`,
   `sizeDelta` usw.).

3. **Auswahl der zu übernehmenden Änderungen**  
   - Per Checkbox wählst du einzelne Properties aus.
   - `Mark All` markiert alle aktuell geänderten Eigenschaften für dieses Objekt.
   - `Unmark All` entfernt alle Markierungen für dieses Objekt.

4. **Apply im Play Mode**  
   Der `Apply`-Button im Panel ruft
   `PlayModeChangesTracker.PersistSelectedChangesForAll()` auf.  
   Alle aktuell markierten und tatsächlich geänderten Properties werden in den
   `PlayModeTransformChangesStore` geschrieben.

5. **Wechsel zurück in den Edit Mode**  
   Beim Eintritt in den Edit Mode liest `PlayModeChangesTracker` den Store,
   sucht die betroffenen GameObjects (über Szenenpfad und Objektpfad) und
   überträgt die gespeicherten Werte. Anschließend wird der Store geleert.

---

## Installation & Setup

1. **Ordnerstruktur**  
   Lege die drei Skripte in einen Editor-Ordner (z.B. `Assets/01_Skripts/Editor`).

2. **ScriptableObject-Asset**  
   Das Asset `PlayModeTransformChangesStore.asset` wird bei Bedarf automatisch
   unter dem Pfad

   ```
   Assets/01_Skripts/Editor/PlayModeTransformChangesStore.asset
   ```

   erstellt. Es ist nicht notwendig, es manuell anzulegen.

3. **Unity-Version**  
   Ausgelegt für den Unity Editor mit Unterstützung für `EditorApplication.playModeStateChanged`
   und `UnityEditor.SceneManagement`. Getestet wurde es in neueren Unity-Versionen
   (2019+); ältere Versionen wurden nicht explizit geprüft.

---

## Verwendung

1. Projektkompilierung abwarten, dann eine beliebige Szene öffnen.
2. Play Mode starten.
3. Im Inspector ein GameObject auswählen, dessen Transform/RectTransform du
   im Play Mode verändert hast.
4. Im Header des Inspectors erscheint das Panel **„Play Mode Overrides“**:
   - **Liste der Properties**: zeigt alle veränderten Eigenschaften.
   - **Checkbox**: markiert, welche Eigenschaften später übernommen werden.
   - **`Mark All`**: markiert alle veränderten Eigenschaften für dieses Objekt.
   - **`Unmark All`**: entfernt alle Markierungen für dieses Objekt.
   - **`Apply`**: schreibt sämtliche für alle Objekte selektierten Änderungen
     in den `PlayModeTransformChangesStore`.
5. Play Mode beenden. Beim Wechsel zurück in den Edit Mode werden alle im
   Store gespeicherten Änderungen automatisch auf die entsprechenden
   GameObjects angewendet (inkl. Undo-Unterstützung im Editor).

---

## Unterstützte Eigenschaften

Für normale `Transform`-Komponenten:

- `position` (lokale Position)
- `rotation` (lokale Rotation / Quaternion)
- `scale` (lokale Skalierung)

Für `RectTransform`-Komponenten zusätzlich:

- `anchoredPosition`
- `anchoredPosition3D`
- `anchorMin`
- `anchorMax`
- `pivot`
- `sizeDelta`
- `offsetMin`
- `offsetMax`

---

## Hinweise & Limitierungen

- **Nur Transform-bezogene Änderungen**  
  Das Tool kümmert sich ausschließlich um Transform- bzw. RectTransform-Daten,
  keine anderen Komponenten oder Werte.

- **Objektidentifikation über Pfad**  
  Die Objekte werden über Szenenpfad (`scenePath`) und Objektpfad (`objectPath`)
  gefunden. Wenn du die Hierarchie oder Namen nach dem Play Mode stark änderst,
  kann das Wiederfinden fehlschlagen.

- **Mehrere Szenen**  
  Szenen werden bei Bedarf additiv geöffnet, wenn Änderungen auf Objekte in
  noch nicht geladenen Szenen angewendet werden sollen.

- **Nur im Editor**  
  Die Skripte gehören in einen `Editor`-Ordner und laufen nur im Unity Editor,
  nicht im Build.

---

