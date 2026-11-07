# UX-windows : cahier des charges

Une application Windows **minimaliste** et **légère** pour personnaliser un poste en double écran.
D'abord pour un usage personnel, puis publiée.

## Principes

1. **Consommer le moins possible.** L'écran principal sert à jouer et à regarder des streams : l'app ne doit jamais faire baisser les FPS.
2. **Minimaliste.** Pas d'icônes sur le bureau, juste la barre des tâches. Une interface sobre et sombre.
3. **Pensée pour le double écran.** Écran principal = jeu / vidéo. Écran de gauche = utilitaires.

## Hors périmètre

- Changement des icônes (le bureau reste vide, ça convient).

## Fonctions

### Fond d'écran animé (étape 2)

- Un fond différent par écran.
- Vidéos (mp4, wmv, mov, m4v, mkv, avi) : décodage matériel Media Foundation, sans son, plafonnées à 30 images/s, affichées via DirectComposition (seule méthode visible derrière les icônes sur Windows 11 24H2 et plus). Le rendu s'arrête complètement en pause.
- Images fixes (jpg, png, bmp) : appliquées comme vrai fond d'écran Windows, écran par écran. Aucune ressource consommée. « Retirer » remet le fond d'origine.
- Journal `%APPDATA%\UXWindows\journal.txt` (ouvrable depuis le menu) et bulle de notification en cas d'erreur.
- Choix du fond de chaque écran depuis l'icône de notification, mémorisé dans `%APPDATA%\UXWindows\config.json`.
- **Pause automatique** de tous les fonds quand une application est en plein écran (jeu, Twitch en plein écran) ou quand le PC est sur batterie.
- Lecture en boucle sans coupure.
- État visible dans le menu de l'icône (en lecture, ou en pause avec la raison).
- Pause manuelle depuis le menu.
- En quittant, le fond Windows d'origine est rétabli.

### Panneau utilitaire de l'écran de gauche (étape 3)

Ancré sur toute la hauteur de l'écran de gauche, fond sombre.

| Widget | Contenu |
|---|---|
| Horloge | Heure et date |
| Processeur (CPU) | Température, consommation en watts, charge en % |
| Carte graphique (GPU) | Température, consommation en watts, charge en % |
| Apple Music | Titre en cours, pochette, lecture / pause / suivant / précédent |
| Claude Code | Bouton de lancement, et suivi des sessions (tâche terminée, attend une réponse) |

Notes techniques :
- Températures et watts : bibliothèque **LibreHardwareMonitor** 0.9.6, lue toutes les 2 s. La carte graphique marche sans droits. Le processeur demande le pilote **PawnIO** et des droits administrateur : l'app lance alors une copie d'elle-même en mode `--capteurs` (administrateur) qui écrit ses lectures dans `%LOCALAPPDATA%\UXWindows\capteurs.json` ; l'app principale reste en mode normal.
- Claude Code : lecture des journaux de session `%USERPROFILE%\.claude\projects\**\*.jsonl` (sessions lancées sur ce PC uniquement). « En cours » si le fichier a bougé il y a moins de 20 s, « attend ta réponse » si le dernier message est de Claude. Le bouton ouvre l'app Claude, sinon `claude` dans Windows Terminal, sinon claude.ai/code.
- Tous les rafraîchissements s'arrêtent quand le panneau est masqué.
- Apple Music : via les contrôles média de Windows (`GlobalSystemMediaTransportControls`), qui marchent avec l'app Apple Music de Windows.

### Réglages et publication (étape 4)

- Fenêtre de réglages (choix des fonds, des widgets, de l'écran utilitaire).
- Lancement au démarrage de Windows.
- Installateur, puis publication (GitHub Releases, plus tard Microsoft Store).

## Choix techniques

- **C# / .NET 8 / WPF** : natif, léger, accès direct aux API Windows.
- Une icône dans la zone de notification (à côté de l'horloge) pour quitter ou ouvrir les réglages.
- Compilation automatique par **GitHub Actions** : chaque modification produit un `.exe` téléchargeable, sans rien installer.
