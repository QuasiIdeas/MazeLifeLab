# BallPark

  + - - - + - + - -
  + - + - + copyright by Vladimir Baranov (Kvazikot)  <br>
  + - + - + email: vsbaranov83@gmail.com  <br>
  + - + - + github:  https://github.com/Quasikot/BallPark <br>
```
                            )            (
                           /(   (\___/)  )\
                          ( #)  \ ('')| ( #
                           ||___c\  > '__||
                           ||**** ),_/ **'|
                     .__   |'* ___| |___*'|
                      \_\  |' (    ~   ,)'|
                       ((  |' /(.  '  .)\ |
                        \\_|_/ <_ _____> \______________
                         /   '-, \   / ,-'      ______  \
                b'ger   /      (//   \\)     __/     /   \
                                            './_____/
```              
# The initial goal of the project:
Make a robots that can learn how to go through the maze.
I'm using machine learning approach i.e. reinforcement learning and imitation learning to achieve this goal.
In the first phases of the current project in 2020 than coronavirus was a popular thing I began to work on RRT trees.
First idea was to create car-like robots that can park in particular slots. At that time this project goal was inspired by my job at that time in the Luxoft company.
I discovered and implement Rapid Exploring Random Trees. But they  was really stone-like in the machine learning sense.
So I started to find ways how I can extend the rrt idea. I wrote a little hack that spread the reward across the RRT in the maze.
When I start to train agents to get a reward like checkpoints in the race. This gives me initial results.
But the agents were so dumb. They frequently hit the walls. I dont realise that was the failure related to my not outstanding experience in machine learning.
I use reinforcement learning instead of imitation learning. 
For training the bots i'm using a machine learning environment provided by mlagents developers.
In the end of a day I decided to delete my old repository and start the project from scratch.
Just rewrite the code, fix bugs implemented in the foundation. 
It helps also to set new goals for the project. Now i'm fan of the artificial life science.
According to some experts this is the true path to achieve AGI. 

# Current goal (22/07/2023)
Idea is to localize and simpilfy the RRT building procces in terms of exploration and number of operations. Get rid of slow runge-kutta calculations. And give agents ability to send soundwaves for communication.
So this would be imitation of social interaction when agents naturally find the way to communicate with each other and how to find a way through the maze.
  
Литература:
* Nonlinear Dynamics And Chaos With Applications To Physics, Biology, Chemistry, And Engineering by Steven H. Strogatz
* Rapidly-Exploring Random Trees: Progress and Prospects
* Unity Machine Learning Agents
* Turgut, A. E., Çelikkanat, H., Gökçe, F., & Şahin, E. (2008). Self-organized flocking in mobile robot swarms. Swarm Intelligence, 2, 97–120.
* Trianni, V., & Dorigo, M. (2006). Self-organisation and communication in groups of simulated and physical robots. Biological Cybernetics, 95, 213–231.
* Baldassarre, G., Trianni, V., Bonani, M., Mondada, F., Dorigo, M., & Nolfi, S. (2007). Self-organized coordinated motion in groups of physically connected robots
* [Self-Organization and Artificial Life](https://direct.mit.edu/artl/article/26/3/391/93243/Self-Organization-and-Artificial-Life)
  
