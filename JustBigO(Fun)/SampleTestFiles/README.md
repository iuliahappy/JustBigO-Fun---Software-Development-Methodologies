# How test files work

## 1. Where is it stored?

**The files themselves are not kept.** Here's what happens:
1. When you upload `.in` and `.out` files, the app **reads the text content** from them
2. This content is saved in the **ProblemTests** table in the database:
   - `InputJson` = content of the .in file
   - `ExpectedOutputJson` = content of the .out file
3. The original files are not stored anywhere on the server – only their text

Users **do not have access** to this data. It will be used when Run/Submit is implemented (to verify solutions).

---

## 2. How do you add multiple tests?

### Step 1: Create the files on your computer
Create pairs of files – each `.in` with its matching `.out`:

```
test1.in    →  input for test 1
test1.out   →  expected output for test 1

test2.in    →  input for test 2
test2.out   →  expected output for test 2

test3.in    →  input for test 3
test3.out   →  expected output for test 3
```

### Step 2: In the Admin form
1. Go to **Admin** → **Problems** → **Create** (or **Edit** on a problem)
2. Scroll down to **"Test files (.in / .out)"**
3. At **".in files"** – select ALL .in files (Ctrl+Click: test1.in, test2.in, test3.in)
4. At **".out files"** – select ALL .out files (test1.out, test2.out, test3.out)
5. Click **Save**

### Step 3: Automatic pairing
The app pairs them by **alphabetical order**:
- first .in (after sorting) ↔ first .out
- second .in ↔ second .out
- etc.

**Important:** The number of .in files must match the number of .out files. If you have 3 .in and 2 .out, only the first 2 pairs will be saved.

---

## 3. Concrete example (Two Sum)

**test1.in:**
```
2 7 11 15
9
```

**test1.out:**
```
[0,1]
```

**test2.in:**
```
3 2 4
6
```

**test2.out:**
```
[1,2]
```

Create these 4 files, select them in the form (test1.in + test2.in for .in, test1.out + test2.out for .out), save – and you have 2 tests in the database.
