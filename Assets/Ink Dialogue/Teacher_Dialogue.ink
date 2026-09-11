=== Teacher_Countdown ===
Alright. #speaker:Teacher
Since you've been failing in your other subjects, you NEED to ACE this test to pass the grade.
I hope you studied hard for this.
... #speaker:Player
(I haven't, I've been playing too much League of Legends because we're at the end of the ranked season.)
(But it's just a few questions right? How bad can it be?)
You only have to complete a few questions in a few minutes. #speaker:Teacher
A FEW!? HOW MUCH IS A FEW? #speaker:Player
[speed=0.4]3... #speaker:Teacher
Do I have to use it? #speaker:Player
[speed=0.4]2... #speaker:Teacher
The Watch my uncle gave me? #speaker:Player
[speed=0.4]1... #speaker:Teacher
He said only in emergencies, this is it! Right!? #speaker:Player
-> TestBegin

= TestBegin
You may now open your tests and begin! #speaker:Teacher
~ OnTestBegin()
~ testBegin = true
-> Main

=== caught_out_of_seat ===
// Player caught outside of seat while time is normal
HEY! #speaker:Teacher
NO LEAVING YOUR SEAT DURING EXAM TIME, PRINCIPLE'S OFFICE, NOW!
...#speaker:Player
Fuc-
~ ExamEndResult(false)
-> DONE

=== test_time_runs_out ===
// Test time runs out (Lose Condition)
PENS DOWN! #speaker:Teacher
You may pass your tests to the front! 
Aww man...I didn't even get to finish..I'm done for.. #speaker:Player
~ ExamEndResult(false)
-> DONE

=== interact_with_teacher ===
// Played when player interacts with teacher during timestop
Yeah, let's not interact with the teacher at this moment. #speaker:Player
-> DONE