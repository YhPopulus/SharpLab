import languages from './languages.js';

const targets = {
    csharp:  languages.csharp,
    vb:      languages.vb,
    il:      'IL',
    asm:     'JIT ASM',
    ast:     'AST',
    run:     'Run',
    verify:  'Verify',
    explain: 'Explain'
};
export default Object.freeze(targets);