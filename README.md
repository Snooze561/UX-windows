# UX-windows

Une application Windows minimaliste pour le double écran :
fond d'écran animé économe en ressources, et un panneau utilitaire sur l'écran de gauche
(horloge, températures et consommation du CPU et du GPU, Apple Music).

Le détail est dans le [cahier des charges](docs/CAHIER_DES_CHARGES.md).

## Essayer l'application (sans rien installer)

1. Ouvre l'onglet **Actions** du dépôt sur GitHub.
2. Clique sur la dernière exécution **Compilation** marquée d'une coche verte.
3. En bas de la page, télécharge **UXWindows** (un fichier `.zip`).
4. Décompresse-le et lance `UXWindows.exe`.
   Windows peut afficher « Windows a protégé votre ordinateur » : clique sur **Informations complémentaires**, puis **Exécuter quand même**. C'est normal pour une app pas encore signée.

Une icône apparaît à côté de l'horloge de la barre des tâches. Clic droit dessus pour :
- choisir le fond (vidéo ou image) de chaque écran ;
- mettre les fonds en pause ;
- masquer le panneau ou quitter.

Les fonds animés se mettent en pause tout seuls quand un jeu ou une vidéo passe en plein écran.

## Le panneau de gauche

- **Système** : température, consommation (watts) et charge du processeur et de la carte graphique.
- **Musique** : morceau en cours dans Apple Music (ou toute autre app de musique), avec pochette et boutons.
- **Claude Code** : tes sessions Claude Code sur ce PC (en cours, ou en attente de ta réponse) et un bouton pour ouvrir Claude.

### Activer la température du processeur (une seule fois)

Windows protège ces capteurs : il faut un petit pilote gratuit, **PawnIO**.

1. Ouvre le menu Démarrer, tape `terminal`, puis lance **Terminal**.
2. Colle cette ligne et appuie sur Entrée : `winget install namazso.PawnIO`
3. Dans le panneau, clique sur le texte orange sous « Processeur » et accepte la demande de Windows.

La carte graphique, elle, s'affiche sans rien installer.

## Avancement

- [x] Étape 1 : cadrage et squelette (panneau de gauche avec horloge, icône de notification)
- [x] Étape 2 : fond d'écran animé avec pause automatique en plein écran
- [x] Étape 3 : widgets CPU / GPU (températures, watts), Apple Music et Claude Code
- [ ] Étape 4 : réglages, lancement au démarrage, installateur
