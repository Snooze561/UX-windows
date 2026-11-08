## Installer

1. Télécharge **`UX-windows-Setup-….exe`** ci-dessous et lance-le (aucun droit administrateur demandé).
2. Si Windows affiche « Windows a protégé votre ordinateur » : **Informations complémentaires**, puis **Exécuter quand même**.

## Pour voir les ventilateurs : installe aussi EZFAN

La section « Ventilateurs » du panneau affiche ce que fait **EZFAN**, l'app qui pilote les ventilateurs. Sans EZFAN, la section indique simplement « EZFAN n'est pas lancé ».

1. Installe le pilote gratuit **PawnIO** (une seule fois, il sert aussi à la température du processeur) : ouvre **Terminal** et tape `winget install namazso.PawnIO`.
2. Télécharge **`EZFAN.exe`** sur [la page des versions d'EZFAN](https://github.com/Snooze561/EZFAN/releases) et lance-le. Suis l'assistant pour nommer tes ventilateurs.
3. Dans EZFAN, active le lancement avec Windows pour ne plus avoir à le démarrer.

Ensuite, le panneau d'UX-windows affiche la vitesse de chaque ventilateur, permet de changer de profil (Bureautique, Jeux légers, Jeux AAA, Performance) et ouvre EZFAN d'un clic.
