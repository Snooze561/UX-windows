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
- Formats : vidéo (mp4) décodée par la carte graphique, en priorité. Images fixes en secours.
- Limite d'images par seconde réglable (30 par défaut).
- **Pause automatique** quand une application est en plein écran (jeu, Twitch en plein écran) ou quand le PC est sur batterie.

### Panneau utilitaire de l'écran de gauche (étape 3)

Ancré sur toute la hauteur de l'écran de gauche, fond sombre.

| Widget | Contenu |
|---|---|
| Horloge | Heure et date |
| Processeur (CPU) | Température, consommation en watts, charge en % |
| Carte graphique (GPU) | Température, consommation en watts, charge en % |
| Apple Music | Titre en cours, pochette, lecture / pause / suivant / précédent |

Notes techniques :
- Températures et watts : bibliothèque **LibreHardwareMonitor** (nécessite de lancer l'app en administrateur pour lire certains capteurs).
- Apple Music : via les contrôles média de Windows (`GlobalSystemMediaTransportControls`), qui marchent avec l'app Apple Music de Windows.

### Réglages et publication (étape 4)

- Fenêtre de réglages (choix des fonds, des widgets, de l'écran utilitaire).
- Lancement au démarrage de Windows.
- Installateur, puis publication (GitHub Releases, plus tard Microsoft Store).

## Choix techniques

- **C# / .NET 8 / WPF** : natif, léger, accès direct aux API Windows.
- Une icône dans la zone de notification (à côté de l'horloge) pour quitter ou ouvrir les réglages.
- Compilation automatique par **GitHub Actions** : chaque modification produit un `.exe` téléchargeable, sans rien installer.
