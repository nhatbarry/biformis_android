// =====================================================================
// BIFORMIS - Main story, English translation of Biformis_Story.ink.
// Keep knot names, variables and tags identical to the Vietnamese file; only the lines are translated.
// =====================================================================

VAR collapseMinutes = 2
VAR corridorBarkIndex = 0


=== Intro ===
Damn it! We're back here again? #speaker:B #bg:black #cast:A,B
I thought we'd run so far away. #speaker:A
Hello there! We meet again! #speaker:Villain #fx:red
Turn us back! I don't want to do this anymore! #speaker:B
We'll give you your money back! Please, let us go! #speaker:A
Sorry! I'm afraid I can't let the two of you go right now. #speaker:Villain
Besides, I have no way of separating you two.
What? #speaker:B
I have an idea. Why don't you two just accept it? #speaker:Villain
What good does it do to keep suffering over it?
You lunatic! You're the one who did all this. Stop preaching at us! #speaker:B
How shallow. Let me tell you, listening to me will gain you far more than you lose. #speaker:Villain
Very well, I'll give you two one more chance.
Don't think of this as a punishment. Think of it as a lesson I've prepared for you both.
System starting up. #speaker:System #cast:none #fx:flash
BEGIN.
-> DONE


=== Bark_Hurt ===
It hurts. #speaker:B
-> DONE

=== Bark_Pain ===
{~I'm so tired.|I don't want to run anymore.|Please.} #speaker:B
-> DONE


=== Level2_End ===
-> WhiteRoom_1

=== WhiteRoom_1 ===
Are you okay? #speaker:Teen #bg:white #cast:B_Bed,Teen #move:Teen:-84 #wait:1.8 #anim:B_Bed:wake #wait:2.6
Who are you? What are you doing here? #speaker:B
I got injured training for the school track team. #speaker:Teen
The doctor said I couldn't run anymore, but I kept trying anyway.
I ran and fell more times than I can count. It hurt so much. And then I ended up here.
You don't know why you're here? #speaker:B
No. But it's so warm and comfortable here. #speaker:Teen
I feel like I could run as fast as flying here.
Wouldn't it be great if it were like this out there too?
Getting shot must really hurt, huh? #speaker:Villain #anim:Teen:vanish #wait:0.7 #cast:B_Bed,Villain #bg:F4D6D6:1 #wait:0.6
What do you actually want? #speaker:B
Who knows. I just hope you come to see the truth soon. #speaker:Villain
Follow me and you won't lose a thing.
What do you mean? #speaker:B
All human suffering comes from the helplessness of being unable to do what lies beyond the body. #speaker:Villain #bg:EBB5B5:1.5
After some research, I realized something. #bg:D68284:1.5
In its final stage of evolution, humanity will merge into one, sharing a single mind, with no physical body. #bg:C8666B:1.5
Then we can never be hurt. We can fly like the wind, travel among the stars, and our possibilities will be endless. #bg:A8434B:1.5
No more helplessness, no more regret, no more discrimination. #bg:86262F:1.5
Then even getting shot wouldn't be scary, would it? #bg:641621:1.5
In short, my mission is to bring that day closer to mankind.
Would you believe that with just one switch, I could give you that dream life?
What is he even talking about... #speaker:B
#anim:Villain:walk #move:Villain:60:2 #wait:2 #anim:Villain:inject #anim:B_Bed:injected:0.8 #wait:2.2 #bg:310910:1 #fx:fade_black #wait:1.5
-> DONE


=== AfterLevel3 ===
I had a really strange dream just now. #speaker:B #bg:black #cast:A,B
Did you dream about those things again? #speaker:A
How do you know what I dreamed about? #speaker:B
Back in high school you got injured and had to quit the school track team, remember? #speaker:A
Did you forget?
[speed=0.4]... #speaker:B
Looks like he wants me to forget all that...
So do you want to forget? #speaker:A
[speed=0.4]... #speaker:B
-> WhiteRoom_2

=== WhiteRoom_2 ===
<i>(B dreams again. The teenager appears.)</i> #speaker:Narrator #bg:white #cast:B,Teen
You remember me now? #speaker:Teen
Yeah. #speaker:B
So how do you feel? #speaker:Teen
I don't know what I'm supposed to feel. I'm just tired. #speaker:B
Are you going to forget me? #speaker:Teen
I don't know. #speaker:B
Stop hesitating. Do you want to go back to being that lonely failure? #speaker:Villain #fx:red
Stop talking. #speaker:B
It's just one press of a button. Come with me already. #speaker:Villain
Hey. Are you there? #speaker:A
A? #speaker:B
Don't listen to him. Please. #speaker:A
Get lost! Do you want to watch him suffer? #speaker:Villain #fx:shake
Open the door for me. #speaker:A
Still so stubborn? Then die. #speaker:Villain #fx:shake
-> Hospital_1

=== Hospital_1 ===
<i>(Darkness. The sound of hospital machines.)</i> #speaker:Narrator #bg:black #cast:none #sfx:beep
Can my boy wake up, doctor? #speaker:Mom #bg:hospital #cast:Mom,Doctor
His mind is resisting very strongly. #speaker:Doctor
At this point, I can't say whether A will be able to get in or not.
-> DONE


=== Level4_End ===
<i>(A appears in the white room and sees B.)</i> #speaker:Narrator #bg:white #cast:A,B
<i>(A hands B a black, square box.)</i> #cast:A,B,Box
-> Past

=== Past ===
<i>(The past.)</i> #speaker:Narrator #bg:past #cast:B #fx:fade_in
Hey! Come eat, dinner's ready! #speaker:A
Hey...
Why do you think we've been like this since the day we were born? #speaker:B
The kind of thing that can get hurt at any moment.
Who are you talking to? #speaker:A
It would be so nice not to have this body. #speaker:B
What's wrong with you? Don't scare me! #speaker:A
The kids at school were talking about me again. #speaker:B
Ugh, I want to split my head open to show them how much they've made me suffer.
Why are there so many people in this world? Why is everyone so different?
It's not fair! Why is everyone after me?
Get out! Get out! #fx:shake
Hey! Are you okay?! #speaker:A #sfx:thud #fx:shake #cast:none
-> Hospital_2

=== Hospital_2 ===
<i>(The sound of hospital machines.)</i> #speaker:Narrator #bg:black #sfx:beep
It seems your son has created a second personality. #speaker:Doctor #bg:hospital #cast:Mom,Doctor
W... what do you mean, doctor? #speaker:Mom
This second personality can make his wishes come true. #speaker:Doctor
So, I'm sorry to say it has taken control.
Your son no longer cares about his real life.
-> WhiteRoom_3

=== WhiteRoom_3 ===
<i>(Back in the white room. A and B face each other.)</i> #speaker:Narrator #sfx:stop #bg:white #cast:A,B
It's been a few years since then. #speaker:A
Then one day the doctor said you were showing signs of improvement.
They took the chance to help me get into your mind and bring you back.
Do you remember? When I was little, I nearly drowned at the beach, and I've been afraid ever since.
[speed=0.4]... #speaker:B
But I don't regret it one bit, because from then on you always stayed right beside me whenever we went swimming. #speaker:A
Seeing you like that made me so happy. I don't want the caring brother who always worried I'd drown to disappear.
No matter what, those memories are what made me who I am now.
Isn't it beautiful, the things that make us different?
I just want to be myself, and I want you to be yourself too, even if it hurts a little.
Let's go home. I'll help you through this, like you once helped me. I believe you can do it.
<i>(The two hold hands.)</i> #speaker:Narrator #fx:flash
-> DONE


=== Corridor_Bark ===
{corridorBarkIndex:
- 0: There's still one more door? #speaker:A
- 1: This is bad. In {collapseMinutes} minutes this whole place is going to collapse. #speaker:B
- else: We have to hurry. #speaker:A
}
~ corridorBarkIndex = corridorBarkIndex + 1
-> DONE
