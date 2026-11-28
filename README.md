# Kartamania

> Jeu de course arcade en **C# / MonoGame** : karts dessinés en code, objets, IA, visages personnalisés, six pistes thématiques et un championnat en six manches.

[Voir le dépôt GitHub](https://github.com/StormACE/Kartamania)

---

## Aperçu

| Élément | Détail |
| --- | --- |
| Langage | C# |
| Framework | .NET 10 |
| Moteur | MonoGame DesktopGL 3.8.4 |
| Rendu | 2D procédural, sans Content Pipeline |
| Modes | Course rapide et championnat |
| Entrée | Clavier et manette (avec vibration) |
| Langues | Français / Anglais |
| Sauvegarde | `settings.txt` |
| Build recommandé | `Release` / `x64` |

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Démarrage rapide](#démarrage-rapide)
- [Commandes](#commandes)
- [Modes de jeu](#modes-de-jeu)
- [Pistes](#pistes)
- [Gameplay](#gameplay)
- [Personnalisation](#personnalisation)
- [Réglages](#réglages)
- [Ajouter une piste](#ajouter-une-piste)
- [Dépannage](#dépannage)
- [Structure du projet](#structure-du-projet)

---

## Fonctionnalités

- Course arcade de **1 à 10 pilotes** en course rapide, avec visages réels ou pilotes « robot ».
- **Championnat** de 6 manches : 10 pilotes, points par course, classement final et podium.
- Trois niveaux de difficulté avec IA, agressivité et vitesse adaptées.
- Karts entièrement dessinés en code, avec **10 couleurs** sélectionnables.
- Objets : missile à tête chercheuse, mine, turbo, bouclier et cœur de soin.
- Système de vie, collisions entre karts, dérapages, pénalités de raccourci et **départ rocket**.
- Retour manette : la vibration varie selon le type d’impact subi par le joueur.
- Meilleurs temps sauvegardés **par piste, par nombre de tours et par difficulté**.
- Interface complète : menu, sous-menu Paramètres, pause, résultats, classement, podium, HUD suiveur, mini-carte et compteur de FPS.
- Paysages générés : arbres, cactus, buissons, chalets, immeubles et bancs de neige ; chute de neige sur Étoile du Nord.

---

## Démarrage rapide

### Prérequis

- SDK **.NET 10**
- Git
- Visual Studio 2022/2026 avec la charge **Développement .NET Desktop**, ou simplement la CLI `dotnet`

Les dépendances NuGet et les bibliothèques natives SDL2/OpenAL sont restaurées automatiquement.

### Avec la CLI

```powershell
git clone https://github.com/StormACE/Kartamania.git
cd Kartamania
dotnet build Kartamania.csproj -c Release -p:Platform=x64
```

L’exécutable se trouve dans :

```text
bin\x64\Release\net10.0\Kartamania.exe
```

### Avec Visual Studio

1. Ouvre `Kartamania.csproj`.
2. Sélectionne **Release** et **x64**.
3. Lance avec **F5**.

---

## Commandes

### En course

| Action | Clavier | Manette |
| --- | --- | --- |
| Accélérer / freiner | Flèches haut/bas ou `W` / `S` | Gâchettes droite / gauche, `A` / `B` |
| Tourner | Flèches gauche/droite ou `A` / `D` (direction progressive) | Stick gauche |
| Utiliser l’arme | `Espace`, `Ctrl` gauche ou `Entrée` | `X`, `RB` ou `Y` |
| Pause | `Échap` | `Start` |
| Reprendre | `Échap` ou `Entrée` | `Start` |
| Recommencer la course / la manche | `R` | `Y` |
| Retour au menu | `M` | `B` |
| Changer de vue après l’arrivée | `Tab` (`Maj+Tab` pour reculer) | Croix directionnelle gauche / droite |
| Terminer la course après l’arrivée | `Entrée` | `A` |

### Dans les menus et écrans

| Action | Clavier | Manette |
| --- | --- | --- |
| Naviguer / changer une valeur | Flèches | Croix directionnelle ou stick gauche |
| Valider | `Entrée` ou `Espace` | `A` |
| Retour | `Échap` | `B` |
| Changer le mode d’affichage | `F11` ou `Alt+Entrée` | `Y` |
| Manche suivante / afficher le classement | `Entrée` | `A` |
| Quitter le championnat | `Échap` | `B` |
| Classement → podium / menu | `Entrée` / `Échap` | `A` / `B` |

La manette connectée en tant que **Player 1** vibre brièvement lors des impacts du joueur : missile, mine, spin sur collision et contact avec un obstacle. L’intensité et la durée dépendent de l’impact.

---

## Modes de jeu

### Course rapide

La course utilise la piste, le pilote, la couleur, le nombre de tours, la difficulté et le nombre de pilotes sélectionnés dans le menu. Les meilleurs temps sont enregistrés normalement.

### Championnat

Le menu principal propose une entrée **Championnat** juste après **Course rapide**.

- Une course par piste, dans l’ordre du jeu : **6 manches** avec les 6 pistes actuelles.
- Grille fixe de **10 pilotes**, indépendamment du réglage `Pilotes`.
- Barème de points : **25, 18, 15, 12, 10, 8** pour les six premiers, puis 0.
- Classement calculé par total de points, puis par nombre de victoires, puis par nombre de 2es places, etc.
- Écrans dédiés : résultats de manche, classement final et podium des trois premiers.
- Le championnat n’est **pas sauvegardé** : il repart de zéro à chaque lancement ou abandon. `R` / `Y` depuis la pause relance seulement la manche en cours et conserve les points déjà acquis.
- Ajouter une piste dans `Track.cs` ajoute automatiquement une manche au championnat.

---

## Pistes

| Piste | Ambiance | Adhérence (`Grip`) | Particularités |
| --- | --- | --- | --- |
| Circuit Ovale | Gazon vert | `1.0` | Arbres éparpillés hors piste |
| Montagnes du québec | Hiver | `0.70` | Sol enneigé, arbres enneigés, route gris-bleu |
| Rallye Forestier | Terre battue | `0.70` | Chalets, rochers, arbres denses |
| Désert du Sud | Sable | `1.0` | Cactus, buissons et deux couches de décor |
| Étoile du Nord | Forêt enneigée | `0.70` | Flocons, bancs de neige, chalets et arbres enneigés |
| Huit de la Ville | Urbain | `1.0` | Immeubles, croisements et pénalité de raccourci |

Le champ `TrackDef.Grip` dans [Track.cs](Track.cs) contrôle l’adhérence générale du kart : plus la valeur est basse, plus le kart met du temps à suivre la direction voulue et plus il dérape. Les pistes à `0.70` sont donc plus glissantes que celles à `1.0`.

La chute de neige est spécifique à **Étoile du Nord** ; **Montagnes du québec** utilise le même thème hivernal pour le décor et les arbres, sans flocons.

---

## Gameplay

### Armes et obstacles

| Élément | Effet |
| --- | --- |
| Missile | Cherche le kart devant toi |
| Mine | Reste derrière toi et explose au passage |
| Turbo | Accélère immédiatement |
| Bouclier | Bloque la prochaine attaque |
| Cœur | Restaure un point de vie |

Chaque kart commence avec **4 points de vie**. À zéro, il est éliminé.

Les boîtes sont consommées seulement si le kart n’a pas déjà une arme. Un kart hors-piste ne peut pas en ramasser. Les cônes font déraper, et les collisions latérales entre karts peuvent provoquer un spin proportionnel à la vitesse d’impact.

### Départ rocket

Commence à appuyer sur l’accélérateur dans les **0,4 dernières secondes** du compte à rebours pour obtenir un boost de **1,2 seconde** au départ.

### Raccourcis

Le hors-piste ralentit le kart. La pénalité de raccourci, elle, n’est active que sur **Huit de la Ville** : cette piste impose le passage par ses points de contrôle et ses points de croisement.

---

## Personnalisation

### Visages des pilotes

Place tes images dans le dossier `Faces` :

- Formats acceptés : PNG, JPG, JPEG, BMP, GIF
- Le **nom du fichier** devient le nom du pilote
- Les images sont copiées à côté de l’exécutable au lancement
- S’il y a moins de photos que de pilotes sélectionnés, des robots complètent la grille

> Si une photo de téléphone apparaît couchée, pivote-la avant de la placer dans `Faces`.

### Musique

Les fichiers audio sont dans `Music` et sont copiés automatiquement dans le dossier de sortie. Le crédit de la musique courante est affiché dans le menu.

### Images

Le dossier `Images` contient les ressources du jeu, notamment `kart.png` et l’icône Windows `kart.ico`.

---

## Réglages

Le menu principal contient **Course rapide**, **Championnat**, **Piste**, **Joueur**, **Couleur du kart**, **Paramètres** et **Quitter**. Les lignes Piste, Joueur et Couleur du kart changent avec gauche/droite ; `Entrée` sur ces lignes lance aussi une course rapide.

Le sous-menu **Paramètres** permet de modifier :

- la difficulté ;
- le nombre de tours (`1` à `20`) ;
- le nombre de pilotes (`1` à `10`) ;
- la résolution ;
- l’affichage : fenêtre, sans bordure ou plein écran ;
- la VSync ;
- la limite de FPS (`60`, `120`, `144`, `240`, illimité) ;
- la langue ;
- `Retour` pour enregistrer et revenir au menu principal.

Le fichier `settings.txt`, créé à côté de l’exécutable, conserve `width`, `height`, `display`, `fullscreen`, `borderless`, `vsync`, `fps`, `track`, `player`, `kartcolor`, `difficulty`, `laps`, `pilots`, `language` et les meilleurs temps (`best`). Les changements sont généralement écrits au retour dans le menu, au lancement d’une course, avec **Quitter** ou immédiatement pour la langue. Le championnat n’y est pas sauvegardé.

Pour obtenir 120 fps, il faut un écran 120 Hz ou plus et la VSync désactivée.

---

## Ajouter une piste

1. Ouvre [Track.cs](Track.cs).
2. Copie une méthode de `TrackLibrary`, par exemple `Salon()`.
3. Remplace les points de contrôle par une boucle fermée.
4. Ajuste `Width`, `Grip`, `Theme`, les couleurs, les obstacles et les boîtes.
5. Ajoute la nouvelle méthode au tableau retourné par `All()`.

La piste apparaît dans le menu et devient automatiquement une manche supplémentaire du championnat.

<details>
<summary>Champs utiles de TrackDef</summary>

| Champ | Rôle |
| --- | --- |
| `Name` | Nom affiché dans le menu |
| `Width` | Largeur de la route |
| `Grip` | Adhérence ; `1.0` par défaut, `0.70` sur les pistes glissantes |
| `Theme` | `Winter`, `Desert`, `City` ou `Normal` |
| `Forest` / `DenseTrees` | Décor boisé et chalets enneigés |
| `RoadMarks` / `RockDots` | Détails de la route |
| `StrictCourse` / `Checkpoints` / `CrossingPoints` | Validation du tracé et pénalité de raccourci |
| `LaneLimit` | Tolérance latérale quand le tracé est strict |
| `StartFraction` | Position de la ligne de départ |
| `Grass` / `Rough` / `Road` | Couleurs du décor et de la route |

</details>

---

## Dépannage

| Problème | Solution |
| --- | --- |
| Le jeu ne démarre pas | Vérifie que le SDK .NET 10 est installé et restaure les packages NuGet |
| Une photo est couchée | Pivote-la dans un éditeur d’images avant de la mettre dans `Faces` |
| Les FPS sont limités | Désactive la VSync et vérifie la fréquence de ton écran |
| Un réglage est cassé | Supprime `settings.txt`, le jeu en recréera un nouveau |
| Le championnat repart de zéro | C’est normal : les championnats ne sont pas sauvegardés |
| La manette ne vibre pas | Vérifie qu’une manette est connectée en Player 1 ; la vibration ne se déclenche que sur impact |
| Un crash survient | Consulte `crash.log` à côté de l’exécutable |

---

## Structure du projet

```text
Kartamania/
├── Kartamania.csproj        Projet .NET / MonoGame
├── Program.cs               Point d’entrée
├── KartamaniaGame.cs        Boucle, menus, championnat, rendu, HUD et vibrations
├── Championship.cs          Points, classement et manches du championnat
├── Race.cs                  Physique, collisions, IA, objets et impacts
├── Track.cs / TrackShapes.cs Définitions et génération des pistes
├── Kart.cs                  Karts et objets
├── GameSettings.cs          Chargement et sauvegarde des réglages
├── Localization.cs          Textes français / anglais
├── FaceLoader.cs            Chargement des visages
├── DisplayRefresh.cs        Gestion des modes d’affichage
├── AudioLoader.cs / MusicPlayer.cs / Sfx.cs Audio
├── M.cs / PixelFont.cs      Utilitaires
├── Faces/                   Photos des pilotes
├── Images/                  Sprite, icône et ressources graphiques
├── Music/                   Pistes audio
└── settings.txt             Sauvegarde locale
```

---

## Crédits

Développé en C# avec MonoGame. Les visages proviennent des fichiers ajoutés dans `Faces`, et les pistes audio sont celles présentes dans `Music`.
