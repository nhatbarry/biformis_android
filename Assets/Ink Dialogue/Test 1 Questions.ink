=== ChoseAnswer(result, -> nextKnot) ===
~ currentQuestion++
{result: Yup. Seems right! | Dang it, something felt off about that answer} #speaker:Player
-> nextKnot

=== FirstQuestion ===
VAR foundAnswer1 = false
What is a group of the same species living in the same area called? #speaker:Default
    * [Ecosystem]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> SecondQuestion)
    * [Biome]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> SecondQuestion)
    * {foundAnswer1} [Population]
        ~ OnAnswerChoose(true)
        -> ChoseAnswer(true, -> SecondQuestion)

=== SecondQuestion ===
VAR foundAnswer2 = false
What is the smallest unit of an element that retains its properties? #speaker:Default
    * [Molecule]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> ThirdQuestion)
    * [Atom]
        ~ OnAnswerChoose(true)
        -> ChoseAnswer(true, -> ThirdQuestion)
    * {foundAnswer2} [Compound]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> ThirdQuestion)

=== ThirdQuestion ===
VAR foundAnswer3 = false
What tool is used to measure the volume of a liquid? #speaker:Default
    * [Balance]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> EndTest)
    * [Thermometer]
        ~ OnAnswerChoose(false)
        -> ChoseAnswer(false, -> EndTest)
    * {foundAnswer3} [Graduated Cylinder]
        ~ OnAnswerChoose(true)
        -> ChoseAnswer(true, -> EndTest)
        
// === FourthQuestion ===
// VAR foundAnswer4 = false
// Which Canadian landform region covers the most land area? #speaker:Default
//     * [Canadian Shield]
//         ~ OnAnswerChoose(true)
//         -> ChoseAnswer(true, -> FourthQuestion)
//     * [Rocky Mountains]
//         ~ OnAnswerChoose(false)
//         -> ChoseAnswer(false, -> FourthQuestion)
//     * {foundAnswer4} [Arctic Lowlands]
//         ~ OnAnswerChoose(false)
//         -> ChoseAnswer(false, -> FourthQuestion)
        
// === FifthQuestion ===
// VAR foundAnswer5 = false
// A farmer has 17 sheep. All but 9 die. How many sheep does the farmer have left? #speaker:Default
//     * [8]
//         ~ OnAnswerChoose(false)
//         -> ChoseAnswer(false, -> FifthQuestion)
//     * [17]
//         ~ OnAnswerChoose(false)
//         -> ChoseAnswer(false, -> FifthQuestion)
//     * {foundAnswer4} [9]
//         ~ OnAnswerChoose(true)
//         -> ChoseAnswer(true, -> FifthQuestion)
        
        
=== EndTest ===
{playerHp > 1:
    -> passed_exam
  - else:
    -> failed_exam
}
-> DONE


