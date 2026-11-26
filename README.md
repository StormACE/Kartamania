# Kartamania

> Jeu de course arcade en **C# / MonoGame**, avec karts dessinés en code, objets, IA, visages personnalisés et six pistes thématiques.

[Voir le dépôt GitHub](https://github.com/StormACE/Kartamania)

---

## Aperçu

| Élément | Détail |
| --- | --- |
| Langage | C# |
| Framework | .NET 10 |
| Moteur | MonoGame DesktopGL 3.8 |
| Rendu | 2D procédural, sans Content Pipeline |
| Entrée | Clavier et manette |
| Langues | Français / Anglais |
| Sauvegarde | `settings.txt` |
| Build recommandé | `Release` / `x64` |

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Démarrage rapide](#démarrage-rapide)
- [Commandes](#commandes)
- [Pistes](#pistes)
- [Gameplay](#gameplay)
- [Personnalisation](#personnalisation)
- [Réglages](#réglages)
- [Ajouter une piste](#ajouter-une-piste)
- [Dépannage](#dépannage)
- [Structure du projet](#structure-du-projet)

---

## Fonctionnalités

- Course arcade de **1 à 10 pilotes**, avec visages réels ou pilotes « robot ».
- Trois niveaux de difficulté avec IA, agressivité et vitesse adaptées.
- Karts entièrement dessinés en code, avec **10 couleurs** sélectionnables.
- Objets : missile à tête chercheuse, mine, turbo, bouclier et cœur de soin.
- Système de vie, collisions entre karts, dérapages et pénalités hors-piste.
- Meilleurs temps sauvegardés **par piste, par nombre de tours et par difficulté**.
- Interface complète : menu, pause, résultats, HUD suiveur, mini-carte et compteur de FPS.
- Paysages générés : arbres, cactus, buissons, chalets, immeubles, bancs de neige et chute de neige.

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

| Action | Clavier | Manette |
| --- | --- | --- |
| Accélérer / freiner | Flèches ou `W` / `S` | Gâchettes droite / gauche |
| Tourner | Flèches ou `A` / `D` | Stick gauche |
| Utiliser l’arme | `Espace` ou `Ctrl` | `X` ou `RB` |
| Pause | `Échap` | `Start` |
| Recommencer | `R` | `Y` |
| Retour au menu | `M` | `B` |
| Valider / quitter les résultats | `Entrée` | `A` |
| Plein écran | `F11` ou `Alt+Entrée` | — |

---

## Pistes

| Piste | Ambiance | Particularités |
| --- | --- | --- |
| Circuit Ovale | Gazon vert | Arbres éparpillés hors piste |
| Montagnes du québec | Hiver / glace | Adhérence réduite (`Grip = 0.25`) |
| Rallye Forestier | Terre battue | Chalets, rochers, arbres denses (`Grip = 0.50`) |
| Désert du Sud | Sable | Cactus, buissons et deux couches de décor |
| Étoile du Nord | Gazon enneigé | Flocons, bancs de neige, chalets enneigés, glace (`Grip = 0.25`) |
| Huit de la Ville | Urbain | Immeubles, croisements et lampadaires |

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

<details>
<summary>Adhérence hors-piste</summary>

L’adhérence est définie par `TrackDef.Grip` dans [Track.cs](Track.cs). Plus la valeur est faible, plus le kart glisse :

- `1.0` : valeur par défaut
- `0.50` : Rallye Forestier
- `0.25` : Montagnes du québec et Étoile du Nord

</details>

---

## Personnalisation

### Visages des pilotes

Place tes images dans le dossier `Faces` :

- Formats acceptés : PNG, JPG, BMP, GIF
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

Le fichier `settings.txt` est créé à côté de l’exécutable et conserve :

- la résolution et le mode plein écran ;
- la VSync et la limite de FPS (`60`, `120`, `144`, `240`, illimité) ;
- la langue ;
- le nombre de pilotes ;
- la difficulté ;
- la couleur du kart du joueur ;
- les meilleurs temps.

Pour obtenir 120 fps, il faut un écran 120 Hz ou plus et la VSync désactivée.

---

## Ajouter une piste

1. Ouvre [Track.cs](Track.cs).
2. Copie une méthode de `TrackLibrary`, par exemple `Salon()`.
3. Remplace les points de contrôle par une boucle fermée.
4. Ajuste `Width`, `Grip`, `Theme`, les couleurs, les obstacles et les boîtes.
5. Ajoute la nouvelle méthode au tableau retourné par `All()`.

<details>
<summary>Champs utiles de TrackDef</summary>

| Champ | Rôle |
| --- | --- |
| `Name` | Nom affiché dans le menu |
| `Width` | Largeur de la route |
| `Grip` | Adhérence hors-piste |
| `Theme` | `Winter`, `Desert`, `City` ou valeur par défaut |
| `Forest` / `DenseTrees` | Décor boisé |
| `RoadMarks` / `RockDots` | Détails de la route |
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
| Un crash survient | Consulte `crash.log` à côté de l’exécutable |

---

## Structure du projet

```text
Kartamania/
├── Kartamania.csproj        Projet .NET / MonoGame
├── Program.cs               Point d’entrée
├── KartamaniaGame.cs        Boucle, menu, rendu, HUD et effets
├── Race.cs                  Physique, collisions, IA et objets
├── Track.cs / TrackShapes.cs Définitions et génération des pistes
├── Kart.cs                  Karts et objets
├── GameSettings.cs          Chargement et sauvegarde des réglages
├── Localization.cs          Textes français / anglais
├── FaceLoader.cs            Chargement des visages
├── MusicPlayer.cs / Sfx.cs  Audio
├── Faces/                   Photos des pilotes
├── Images/                  Sprite, icône et ressources graphiques
├── Music/                   Pistes audio
└── settings.txt             Sauvegarde locale
```

---

## Crédits

Développé en C# avec MonoGame. Les visages proviennent des fichiers ajoutés dans `Faces`, et les pistes audio sont celles présentes dans `Music`.
