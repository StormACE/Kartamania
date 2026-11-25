**Kartamania**  
Jeu de course façon Mario Kart, en C# avec MonoGame.  
**Lancer le jeu**  
1. Visual Studio 2026 Community avec la charge de travail **Développement .NET Desktop** (le SDK .NET 10 est inclus).  
2. Fichier > Ouvrir > Projet/Solution > choisis `Kartamania.csproj`.  
3. Visual Studio télécharge MonoGame tout seul (NuGet). Appuie sur **F5**.  
**Ajouter les visages**  
Mets les photos dans le dossier `Faces` du projet (PNG, JPG, BMP, GIF), une par personne. Le nom du fichier devient le nom du pilote.  
   
 Elles sont copiées à côté de l'exécutable au lancement. Tu peux aussi les déposer directement dans `bin\Debug\net10.0\Faces`.  
   
  Le menu permet de choisir entre 1 et 10 pilotes. S'il y a moins de photos que de pilotes choisis, des pilotes « robot » complètent la grille ; sinon, les adversaires sont tirés au hasard à chaque course.
   
 Si une photo apparaît couchée (photo de téléphone), pivote-la dans Photos ou Paint et enregistre-la.  
**Commandes**  
  
|  |  |  |  
| - | - | - |  
| **Action** |  **Clavier** |  **Manette** |  
| Accélérer / freiner |  Flèches ou W/S |  Gâchette droite / gauche |  
| Tourner |  Flèches ou A/D |  Stick gauche |  
| Utiliser l'arme |  Espace ou Ctrl |  X ou RB |  
| Pause |  Échap |  Start |  
| Terminer la course après son arrivée |  Entrée |  A |  
| Plein écran |  F11 ou Alt+Entrée |    |  
  
   
**Armes et obstacles**  
Chaque kart a 4 points de vie. À 0, il est éliminé.  
   
 Traverse une boîte dorée pour piocher une arme : **missile** à tête chercheuse, **mine** déposée derrière toi, **turbo**, **bouclier** (bloque le prochain coup).  
   
 Les cônes orange font déraper (sans perte de vie). L'herbe ralentit de moitié.  
**Meilleur temps**  
Le meilleur temps du joueur est sauvegardé pour chaque piste et chaque nombre de tours dans `settings.txt`, et affiché à la fin de la course.  
**Résolution et 120 fps**  
Le menu permet de choisir la résolution (liste des modes de ton écran), plein écran ou fenêtre, VSync et la limite de FPS (60, 120, 144, 240, illimité).  
   
 Le titre de la fenêtre et le coin haut droit affichent les FPS en direct.  
   
 Pour voir 120 fps, il faut un écran 120 Hz ou plus et VSync sur NON.  
**Build Release x64**  
Dans Visual Studio, choisis `Release` et `x64` dans la barre d'outils, ou utilise `dotnet build Kartamania.csproj -c Release -p:Platform=x64`.  
L'exécutable est généré dans `bin\x64\Release\net10.0\`.  
**Ajouter une piste**  
Dans `Track.cs`, copie une méthode de `TrackLibrary` (par ex. `Salon()`), change les points de contrôle (une boucle fermée), les obstacles et les couleurs, puis ajoute-la à `All()`.  
   
 Les réglages sont sauvegardés dans `settings.txt`. En cas de plantage, le détail est écrit dans `crash.log`.  
