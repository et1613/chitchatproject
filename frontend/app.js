require("dotenv").config();

const express = require("express");
const cors = require("cors");
const bcrypt = require("bcrypt");
const jwt = require("jsonwebtoken");
const nodemailer = require("nodemailer");

const transporter = nodemailer.createTransport({
  service: "gmail",
  auth: {
    user: process.env.EMAIL_USER,
    pass: process.env.EMAIL_PASS
  }
});

// Doğrulama e-postası gönderme fonksiyonu
function sendVerificationEmail(email, token) {
  const verifyLink = `${process.env.BACKEND_URL}/api/verify?token=${token}`;
  const mailOptions = {
    from: '"ChitChat" <batuhanakin0067@gmail.com>',
    to: email,
    subject: "E-posta Doğrulama",
    html: `
  <p>Merhaba,</p>
  <p>Hesabınızı doğrulamak için <a href="${verifyLink}">buraya tıklayın</a>.</p>
  <p>Ya da bu bağlantıyı kopyalayın:</p>
  <p>${verifyLink}</p>
`
  };

  transporter.sendMail(mailOptions, (error, info) => {
    if (error) return console.error("Mail gönderilemedi:", error);
    console.log("✅ Doğrulama maili gönderildi:", info.response);
  });
  console.log("🔗 Doğrulama linki:", verifyLink);

}


const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json({ limit: "5mb" })); 
app.use(express.static("public"));
const users = [];

// 🔐 Register endpoint
app.post("/api/register", async (req, res) => {
  const { email, password, name, gender, avatar } = req.body;
  // Email kontrolü
  if (users.find(u => u.email === email)) {
    return res.status(400).json({ message: "Bu e-posta zaten kayıtlı." });
  }

  // Şifreyi hash'le
  const hashedPassword = await bcrypt.hash(password, 10);

  // Kullanıcıyı kaydet
  
  const emailToken = jwt.sign({ email }, "email-secret", { expiresIn: "1h" });

users.push({
  email,
  password: hashedPassword,
  name,
  gender,
  avatar: avatar || null,
  isVerified: false
});

sendVerificationEmail(email, emailToken);

res.json({ message: "Kayıt başarılı. Lütfen e-postanızı doğrulayın." });

});

app.post("/api/login", async (req, res) => {
  const { email, password } = req.body;

  const user = users.find(u => u.email === email);
  if (!user) {
    return res.status(401).json({ message: "E-posta bulunamadı." });
  }

  // Doğrulama kontrolü
  if (!user.isVerified) {
    return res.status(403).json({ message: "Lütfen e-postanızı doğrulayın." });
  }

  const valid = await bcrypt.compare(password, user.password);
  if (!valid) {
    return res.status(401).json({ message: "Şifre hatalı." });
  }

  const token = jwt.sign({ email }, "gizli-anahtar", { expiresIn: "2h" });

  res.json({
    token,
    user: {
      name: user.name,
      gender: user.gender,
      email: user.email
    }
  });
});

// Token doğrulama (korumalı endpoint)
app.get("/api/me", (req, res) => {
  const authHeader = req.headers.authorization;
  const token = authHeader?.split(" ")[1];

  if (!token) return res.status(401).json({ message: "Token eksik." });

  try {
    const decoded = jwt.verify(token, "gizli-anahtar");
    res.json({ email: decoded.email });
  } catch (err) {
    res.status(403).json({ message: "Geçersiz token." });
  }
});
app.get("/api/verify", (req, res) => {
  const token = req.query.token;

  try {
    const decoded = jwt.verify(token, "email-secret");
    const user = users.find(u => u.email === decoded.email);

    if (!user) return res.status(400).send("Kullanıcı bulunamadı.");
    user.isVerified = true;

    res.send(`
  <h2 style="color: green;">✅ E-posta doğrulandı!</h2>
  <p>Artık <a href="${process.env.BACKEND_URL}/index.html">giriş yapabilirsiniz</a>.</p>
`);


  } catch (err) {
    res.status(400).send("❌ Doğrulama linki geçersiz veya süresi dolmuş.");
  }
});

const messages = []; // Geçici mesaj listesi bellekte tutulur
//Tüm kullanıcıları döner (isim, cinsiyet ve e-posta dahil)
app.get("/api/users", (req, res) => {
  const userList = users.map(u => ({
  name: u.name,
  gender: u.gender,
  email: u.email,
  avatar: u.avatar || null
}));
  res.json(userList);
});
// 📩 Mesaj gönderme endpointi
app.post("/api/messages", (req, res) => {
  const { sender, receiver, text, timestamp, gender, fileData, fileName, fileType, avatar } = req.body;
  if (!sender || !receiver || (!text && !fileData)) {
    return res.status(400).json({ message: "Eksik mesaj verisi" });
  }

  const newMessage = {
    sender,
    receiver,
    text,
    avatar: avatar || null,
    timestamp: timestamp || new Date().toLocaleString(),
    gender,
    fileData: fileData || null,
    fileName: fileName || null,
    fileType: fileType || null
  };

  messages.push(newMessage);
  res.status(201).json({ message: "Mesaj kaydedildi" });
});

// Mesajları getirme endpointi
app.get("/api/messages", (req, res) => {
  const { user1, user2 } = req.query;
  if (!user1 || !user2) return res.status(400).json({ message: "Kullanıcılar eksik" });

  const filtered = messages.filter(m =>
    (m.sender === user1 && m.receiver === user2) ||
    (m.sender === user2 && m.receiver === user1)
  );

  res.json(filtered);
});  

// Test
app.get("/", (req, res) => {
  res.send("ChitChat API çalışıyor");
});

app.listen(PORT, () => {
  console.log(`✅ Server ${process.env.BACKEND_URL || "http://localhost:3000"} üzerinden çalışıyor`);
});
