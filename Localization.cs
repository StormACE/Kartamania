using System.Collections.Generic;

namespace Kartamania
{
    public enum GameLanguage { French, English }

    public static class Loc
    {
        static GameLanguage language = GameLanguage.French;

        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "DIFFICULTY_0", new[] { "FACILE", "EASY" } },
            { "DIFFICULTY_1", new[] { "NORMAL", "NORMAL" } },
            { "DIFFICULTY_2", new[] { "DIFFICILE", "HARD" } },
            { "PLAYER", new[] { "JOUEUR", "PLAYER" } },
            { "ROBOT", new[] { "ROBOT", "BOT" } },
            { "HINT_MENU_PAD_1", new[] { "STICK G OU CROIX: CHOISIR   GAUCHE/DROITE: CHANGER   A: VALIDER", "LEFT STICK OR D-PAD: SELECT   LEFT/RIGHT: CHANGE   A: CONFIRM" } },
            { "HINT_MENU_PAD_2", new[] { "Y: PLEIN ECRAN", "Y: FULLSCREEN" } },
            { "HINT_MENU_KB_1", new[] { "HAUT/BAS: CHOISIR   GAUCHE/DROITE: CHANGER   ENTREE: VALIDER", "UP/DOWN: SELECT   LEFT/RIGHT: CHANGE   ENTER: CONFIRM" } },
            { "HINT_MENU_KB_2", new[] { "F11: PLEIN ECRAN   ECHAP: QUITTER", "F11: FULLSCREEN   ESC: QUIT" } },
            { "HINT_RACE_PAD", new[] { "STICK G: TOURNER   GACHETTE D: ACCELERER   GACHETTE G: FREINER   X: ARME   START: PAUSE", "LEFT STICK: STEER   RT: ACCELERATE   LT: BRAKE   X: ITEM   START: PAUSE" } },
            { "HINT_RACE_KB", new[] { "FLECHES/WASD: CONDUIRE   ESPACE: ARME   ECHAP: PAUSE", "ARROWS/WASD: DRIVE   SPACE: ITEM   ESC: PAUSE" } },
            { "HINT_PAUSE_PAD_1", new[] { "START: REPRENDRE", "START: RESUME" } },
            { "HINT_PAUSE_PAD_2", new[] { "Y: RECOMMENCER", "Y: RESTART" } },
            { "HINT_PAUSE_PAD_3", new[] { "B: MENU", "B: MENU" } },
            { "HINT_PAUSE_KB_1", new[] { "ECHAP: REPRENDRE", "ESC: RESUME" } },
            { "HINT_PAUSE_KB_2", new[] { "R: RECOMMENCER", "R: RESTART" } },
            { "HINT_PAUSE_KB_3", new[] { "M: MENU", "M: MENU" } },
            { "HINT_RESULTS_PAD", new[] { "A: MENU   Y: REJOUER", "A: MENU   Y: PLAY AGAIN" } },
            { "HINT_RESULTS_KB", new[] { "ENTREE: MENU   R: REJOUER", "ENTER: MENU   R: PLAY AGAIN" } },
            { "ROW_PLAY", new[] { "JOUER", "PLAY" } },
            { "LABEL_TRACK", new[] { "PISTE", "TRACK" } },
            { "LABEL_PLAYER", new[] { "JOUEUR", "PLAYER" } },
            { "LABEL_DIFFICULTY", new[] { "DIFFICULTE", "DIFFICULTY" } },
            { "LABEL_LAPS", new[] { "TOURS", "LAPS" } },
            { "LABEL_PILOTS", new[] { "PILOTES", "DRIVERS" } },
            { "LABEL_DISPLAY", new[] { "AFFICHAGE", "DISPLAY" } },
            { "LABEL_FPS_LIMIT", new[] { "LIMITE FPS", "FPS LIMIT" } },
            { "FULLSCREEN", new[] { "PLEIN ECRAN", "FULLSCREEN" } },
            { "WINDOW", new[] { "FENETRE", "WINDOW" } },
            { "YES", new[] { "OUI", "YES" } },
            { "NO", new[] { "NON", "NO" } },
            { "UNLIMITED", new[] { "ILLIMITE", "UNLIMITED" } },
            { "QUIT", new[] { "QUITTER", "QUIT" } },
            { "LANGUAGE", new[] { "LANGUE", "LANGUAGE" } },
            { "SUBTITLE", new[] { "COURSE DE KART POUR TOUTE LA FAMILLE", "KART RACING FOR THE WHOLE FAMILY" } },
            { "ADD_PHOTOS", new[] { "AJOUTE TES PHOTOS DANS LE DOSSIER FACES", "ADD YOUR PHOTOS TO THE FACES FOLDER" } },
            { "PHOTO_FORMAT", new[] { "(PNG OU JPG, UN FICHIER PAR PERSONNE)", "(PNG OR JPG, ONE FILE PER PERSON)" } },
            { "ADD_MUSIC", new[] { "AJOUTE TA MUSIQUE DANS LE DOSSIER MUSIC (WAV/OGG)", "ADD YOUR MUSIC TO THE MUSIC FOLDER (WAV/OGG)" } },
            { "MUSIC_COUNT", new[] { "MUSIQUE: ", "MUSIC: " } },
            { "MUSIC_RANDOM", new[] { " FICHIER(S) - UN AU HASARD PAR COURSE", " FILE(S) - ONE AT RANDOM PER RACE" } },
            { "FACES_UNREADABLE", new[] { "DOSSIER FACES ILLISIBLE: ", "FACES FOLDER UNREADABLE: " } },
            { "PHOTOS_UNREADABLE", new[] { " PHOTO(S) ILLISIBLE(S)", " PHOTO(S) UNREADABLE" } },
            { "HUD_LAP", new[] { "TOUR ", "LAP " } },
            { "HUD_HEALTH", new[] { "POINTS DE VIE", "HEALTH" } },
            { "ITEM_SHIELD", new[] { "BOUCLIER", "SHIELD" } },
            { "WAITING", new[] { "EN ATTENTE DES AUTRES PILOTES : ", "WAITING FOR THE OTHER DRIVERS: " } },
            { "RACING", new[] { " EN COURSE", " STILL RACING" } },
            { "END_RACE_PAD", new[] { "A: TERMINER LA COURSE", "A: END THE RACE" } },
            { "END_RACE_KB", new[] { "ENTREE: TERMINER LA COURSE", "ENTER: END THE RACE" } },
            { "VIEW_HINT_PAD", new[] { "DROITE/GAUCHE: CHANGER DE VUE", "LEFT/RIGHT: CHANGE VIEW" } },
            { "VIEW_HINT_KB", new[] { "TAB: CHANGER DE VUE   MAJ+TAB: VUE PRECEDENTE", "TAB: CHANGE VIEW   SHIFT+TAB: PREVIOUS VIEW" } },
            { "VIEW_LABEL", new[] { "VUE: ", "VIEW: " } },
            { "CONTROLS", new[] { "COMMANDES", "CONTROLS" } },
            { "RESULT_ELIMINATED", new[] { "ELIMINE!", "ELIMINATED!" } },
            { "RESULT_VICTORY", new[] { "VICTOIRE!", "VICTORY!" } },
            { "RESULT_FINISHED", new[] { "COURSE TERMINEE", "RACE FINISHED" } },
            { "BEST_TIME", new[] { "MEILLEUR TEMPS ", "BEST TIME " } },
            { "BEST_LAPS", new[] { " TOURS : ", " LAPS: " } },
            { "NEW_RECORD", new[] { "NOUVEAU RECORD!", "NEW RECORD!" } },
            { "ELIMINATED", new[] { "ELIMINE", "ELIMINATED" } },
            { "RESULT_LAP", new[] { "TOUR ", "LAP " } },
            { "FEAT_VICTORY_ELIM", new[] { "VICTOIRE PAR ELIMINATION!", "VICTORY BY ELIMINATION!" } },
            { "MSG_FINISHED", new[] { " TERMINE ", " FINISHED " } },
            { "MSG_BLOCKED", new[] { " BLOQUE ", " BLOCKED " } },
            { "MSG_ELIMINATED", new[] { " ELIMINE ", " ELIMINATED " } },
            { "MSG_SELF_ELIM", new[] { " S'EST ELIMINE!", " ELIMINATED THEMSELVES!" } },
            { "MSG_HIT", new[] { " TOUCHE ", " HIT " } },
            { "MSG_RESPAWN", new[] { " REMIS SUR LA PISTE!", " BACK ON TRACK!" } },
        };

        static readonly Dictionary<string, string> TrackNames = new Dictionary<string, string>
        {
            { "Circuit du Salon", "Living Room Circuit" },
            { "Lac du Chalet", "Cabin Lake" },
            { "Rallye du Sous-sol", "Basement Rally" },
            { "Desert du Sud", "Southern Desert" },
            { "Etoile du Nord", "Northern Star" },
        };

        public static GameLanguage Language => language;

        public static void Set(int value) => language = value == 1 ? GameLanguage.English : GameLanguage.French;

        public static string Get(string key)
        {
            string[] lines;
            return Lines.TryGetValue(key, out lines) ? lines[(int)language] : key;
        }

        public static string TrackName(string name)
        {
            string translated;
            return language == GameLanguage.English && TrackNames.TryGetValue(name, out translated) ? translated : name;
        }

        public static string Ordinal(int n)
        {
            if (language == GameLanguage.French) return n == 1 ? "1ER" : n + "E";
            if (n % 100 >= 11 && n % 100 <= 13) return n + "TH";
            int last = n % 10;
            return n + (last == 1 ? "ST" : last == 2 ? "ND" : last == 3 ? "RD" : "TH");
        }
    }
}
