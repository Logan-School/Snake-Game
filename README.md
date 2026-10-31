# Overview

This project was created with the goal of better understanding all the ways I can use C# in Godot, as well as learning how to build a UI and a functioning game using nearly entirely just the C# language instead of the Godot editor.

The game I created is largely a copy of the popular snake game. I made sprites for the snake, 3 different randomly created fruits, and I used a sound from an opensource website. The way this game works is you play as a snake that's constantly moving, you control the direction it's moving, you try to move into fruits that spawn, and you try not to run into yourself or the walls despite getting longer after every fruit you eat. The goal is to survive as long as possible and eat as many fruit as possible before dying.

The purpose of this software is for the enjoyment of the player and to improve my own skills in making games for the future.

[Software Demo Video] https://youtu.be/JD1GDZx7i1k

# Development Environment

I used a free gulp sound from pixabay. I used ChatGPT to help me understand and implement building the boardstate and getting the project to run in Godot in the first place. I also used it to understand errors and how to alter the syntax to solve the issues. I used the Godot Docs website to better understand how to use C# with Godot.

The programming language for this project was C#. I also used these coding libraries: Godot, System, and System.Collections.Generic

# Useful Websites

- [pixabay] https://pixabay.com/sound-effects/search/gulp/
- [Godot Docs] https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html

# Future Work

If I were to continue working on the project, I'd want to split the game into different nodes and scripts to better handle the game and simplify the updating process. It's much harder to make changes to the game when all of the information is stored in one place with little organization.

- I would like to add music.
- I would like to add the ability to teleport from one wall to the other instead of dying, either as a powerup or as a general game feature.
- I'd like to add a lives system
- I'd like to add obstacles
- I'd like to add levels with differently sized and shaped boards with increasing difficulty
