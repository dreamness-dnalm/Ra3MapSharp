grammar Lua4;

// Entry point
chunk: stat*;

// Block is the same as chunk
block: chunk;

// Statements
stat: varlist1 '=' explist1                     # Assignment
    | callStatement                              # Call
    | DO block END                               # DoBlock
    | WHILE exp1 DO block END                    # WhileLoop
    | REPEAT block UNTIL exp1                    # RepeatLoop
    | IF exp1 THEN block elseifPart* elsePart? END  # IfStatement
    | RETURN explist1?                           # ReturnStatement
    | BREAK                                      # BreakStatement
    | FOR ID '=' exp1 ',' exp1 (',' exp1)? DO block END  # ForLoop
    | FOR ID ',' ID IN exp1 DO block END         # ForInLoop
    | FUNCTION funcname '(' parlist1? ')' block END  # FunctionDef
    | LOCAL declist init?                        # LocalDeclaration
    ;

elseifPart: ELSEIF exp1 THEN block;

elsePart: ELSE block;

// Call statement
callStatement: varPrefix args                    # SimpleCallStmt
             | var '.' ID args                   # DotCallStmt
             | var ':' ID args                   # MethodCallStmt
             | var '[' exp1 ']' args             # IndexedCallStmt
             ;

// Function name
funcname: ID                                     # SimpleName
        | ID '.' ID                              # DotName
        | ID ':' ID                              # ColonName
        ;

// Variable list
varlist1: var (',' var)*;

// Variable - handle left-recursion properly
var: varPrefix                                   # PrefixVar
   | var '[' exp1 ']'                            # IndexedVar
   | var '.' ID                                   # DotVar
   | var ':' ID args                              # MethodVar
   | var args                                     # CallVar
   ;

// Variable prefix (starting point)
varPrefix: ID                                    # SimpleVar
          | upvalue                               # UpvalueVar
          ;

// Upvalue can be used as a starting point for vars

// Declaration list
declist: ID (',' ID)*;

// Initialization
init: '=' explist1;

// Expression list
explist1: exp1 (',' exp1)* ',' exp?
        | exp
        ;

// Expression
exp1: exp;

exp: orExp;

orExp: andExp (OR andExp)*;

andExp: comparisonExp (AND comparisonExp)*;

comparisonExp: concatExp (('<' | '<=' | '>' | '>=' | '==' | '~=') concatExp)*;

concatExp: addExp ('..' addExp)*;

addExp: mulExp (('+' | '-') mulExp)*;

mulExp: powExp (('*' | '/') powExp)*;

powExp: (unaryExp | primaryExp) ('^' powExp)?
      | unaryExp
      | primaryExp
      ;

unaryExp: ('-' | NOT) exp;

primaryExp: NIL                                  # NilExp
           | NUMBER                              # NumberExp
           | STRING                              # StringExp
           | var                                 # VarExp
           | function                            # FunctionExp
           | tableconstructor                    # TableExp
           | '(' exp ')'                         # ParenExp
           ;

// Arguments
args: '(' explist1? ')'                         # ArgsParen
    | tableconstructor                           # ArgsTable
    | STRING                                     # ArgsString
    ;

// Function
function: FUNCTION '(' parlist1? ')' block END;

// Parameter list
parlist1: '...'                                  # VarArgs
        | ID (',' ID)* (',' '...')?              # Params
        ;

// Upvalue
upvalue: '%' ID;

// Table constructor
tableconstructor: '{' fieldlist '}';

fieldlist: lfieldlist                            # ListFields
         | ffieldlist                             # MapFields
         | lfieldlist ';' ffieldlist              # MixedFields1
         | ffieldlist ';' lfieldlist              # MixedFields2
         |                                        # EmptyFields
         ;

lfieldlist: lfieldlist1?;

lfieldlist1: exp (',' exp)* ','?;

ffieldlist: ffieldlist1?;

ffieldlist1: ffield (',' ffield)* ','?;

ffield: '[' exp ']' '=' exp                      # IndexedField
      | ID '=' exp                               # NamedField
      ;

// Lexer rules (keywords must come before ID)
AND: 'and';
BREAK: 'break';
DO: 'do';
ELSE: 'else';
ELSEIF: 'elseif';
END: 'end';
FOR: 'for';
FUNCTION: 'function';
IF: 'if';
IN: 'in';
LOCAL: 'local';
NIL: 'nil';
NOT: 'not';
OR: 'or';
REPEAT: 'repeat';
RETURN: 'return';
THEN: 'then';
UNTIL: 'until';
WHILE: 'while';

ID: LETTER (LETTER | DIGIT | '_')*;

NUMBER: DIGIT+ ('.' DIGIT+)? (('e'|'E') ('+'|'-')? DIGIT+)?;

STRING: '"' (~["\\] | ESC)* '"' | '\'' (~['\\] | ESC)* '\'';

WHITESPACE: [ \t\r\n]+ -> skip;

COMMENT: '--' ~[\r\n]* -> skip;

fragment LETTER: [a-zA-Z];
fragment DIGIT: [0-9];
fragment ESC: '\\' ([btnfr"\\/] | 'u' HEX HEX HEX HEX);
fragment HEX: [0-9a-fA-F];
