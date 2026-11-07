# UX-windows

Une application Windows minimaliste pour le double écran :
fond d'écran animé économe en ressources, et un panneau utilitaire sur l'écran de gauche
(horloge, températures et consommation du CPU et du GPU, Apple Music).

Le détail est dans le [cahier des charges](docs/CAHIER_DES_CHARGES.md).

## Installer l'application

1. Ouvre la page **[Releases](https://github.com/Snooze561/UX-windows/releases)** du dépôt et télécharge `UX-windows-Setup-….exe`.
   (Pour une version de test pas encore publiée : onglet **Actions**, dernière exécution **Compilation** avec une coche verte, puis **UX-windows-Setup** en bas de la page.)
2. Lance l'installateur. Aucun droit administrateur n'est demandé ; coche « Lancer UX-windows avec Windows » si tu veux.
3. Windows peut afficher « Windows a protégé votre ordinateur » : clique sur **Informations complémentaires**, puis **Exécuter quand même**. C'est normal pour une app pas encore signée.

Une icône apparaît à côté de l'horloge de la barre des tâches. Clic droit dessus :
- **Réglages…** : fonds de chaque écran, fluidité des vidéos, écran et sections du panneau, démarrage avec Windows, température du processeur ;
- choisir rapidement un fond, mettre les fonds en pause, masquer le panneau, ouvrir le journal, quitter.

Les fonds animés se mettent en pause tout seuls quand un jeu ou une vidéo passe en plein écran.

Pour désinstaller : *Paramètres Windows > Applications > UX-windows > Désinstaller*.

## Le panneau de gauche

- **Système** : température, consommation (watts) et charge du processeur et de la carte graphique.
- **Musique** : morceau en cours dans Apple Music (ou toute autre app de musique), avec pochette et boutons.
- **Claude Code** : tes sessions Claude Code sur ce PC (en cours, ou en attente de ta réponse) et un bouton pour ouvrir Claude.

### Activer la température du processeur (une seule fois)

Windows protège ces capteurs : il faut un petit pilote gratuit, **PawnIO**.

1. Ouvre le menu Démarrer, tape `terminal`, puis lance **Terminal**.
2. Colle cette ligne et appuie sur Entrée : `winget install namazso.PawnIO`
3. Dans le panneau, clique sur le texte orange sous « Processeur » (ou coche l'option dans les Réglages) et accepte la demande de Windows. C'est la seule fois : ensuite, la température s'affiche à chaque démarrage sans rien demander.

La carte graphique, elle, s'affiche sans rien installer.

## Avancement

- [x] Étape 1 : cadrage et squelette (panneau de gauche avec horloge, icône de notification)
- [x] Étape 2 : fond d'écran animé avec pause automatique en plein écran
- [x] Étape 3 : widgets CPU / GPU (températures, watts), Apple Music et Claude Code
- [x] Étape 4 : réglages, lancement au démarrage, installateur

## Publier une nouvelle version

Crée un tag `vX.Y.Z` (par exemple depuis la page Releases > *Draft a new release*) : GitHub compile l'app et l'installateur et les joint à la release automatiquement.
